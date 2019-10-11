﻿using System;
using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;
namespace CodeGenerator.CecilCodeGenerator
{
    class DefinitionGenerator
    {
        public DefinitionGenerator(ReferenceGenerator referenceGenerator)
        {
            this.ReferenceGenerator = referenceGenerator;
            this.Context = referenceGenerator.Context;
        }

        public ReferenceGenerator ReferenceGenerator { get; }
        public Context Context { get; }

        public Cecil.TypeDefinition CreateEmptyTypeDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            EmptyDefinitionGenerator emptyDefinitionGenerator = new EmptyDefinitionGenerator(Context, ReferenceGenerator);
            var cecilDefinition = emptyDefinitionGenerator.CreateEmptyTypeDefinition(typeDefinition);
            Context.DefinitionMapping.TypesMap[typeDefinition] = cecilDefinition;
            return cecilDefinition;
        }

        public Cecil.MethodDefinition CreateEmptyMethodDefinition(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            EmptyDefinitionGenerator emptyDefinitionGenerator = new EmptyDefinitionGenerator(Context, ReferenceGenerator);
            var cecilDefinition = emptyDefinitionGenerator.CreateEmptyMethodDefinition(methodDefinition);
            Context.DefinitionMapping.MethodsMap[methodDefinition] = cecilDefinition;
            return cecilDefinition;
        }

        public void CompleteTypeDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            DefinitionCompleter definitionCompleter = new DefinitionCompleter(ReferenceGenerator);
            definitionCompleter.Complete(typeDefinition);
        }

        public void CompleteMethodDefinition(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            DefinitionCompleter definitionCompleter = new DefinitionCompleter(ReferenceGenerator);
            definitionCompleter.Complete(methodDefinition);
        }

        // This should be called after types are emptly defined
        // otherwise TypeReference(fieldDefinition.Type) could fail
        public Cecil.FieldDefinition CreateFieldDefinition(AnalysisNet.Types.FieldDefinition fieldDefinition)
        {
            var fieldAttribute = Cecil.FieldAttributes.Public;
            if (fieldDefinition.IsStatic)
                fieldAttribute |= Cecil.FieldAttributes.Static;

            Cecil.TypeReference fieldType = ReferenceGenerator.TypeReference(fieldDefinition.Type);
            Cecil.FieldDefinition cecilField = new Mono.Cecil.FieldDefinition(fieldDefinition.Name, fieldAttribute, fieldType);

            return cecilField;
        }
    }

    internal class InstructionGenerator
    {
        public InstructionGenerator(ReferenceGenerator referenceGenerator)
        {
            this.Context = referenceGenerator.Context;
            this.ReferenceGenerator = referenceGenerator;
        }

        public Context Context { get; }
        public ReferenceGenerator ReferenceGenerator { get; }

        public void CreateInstructions(Model.Types.MethodDefinition methodDefinition,
            Mono.Cecil.MethodDefinition methodDef,
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions,
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions)
        {
            Cecil.Cil.ILProcessor ilProcessor = methodDef.Body.GetILProcessor();
            BytecodeTranslator translator = new BytecodeTranslator(methodDefinition, variableDefinitions, parameterDefinitions, ReferenceGenerator, ilProcessor);

            var instructions = translator.Translate();

            foreach (Mono.Cecil.Cil.Instruction ins in instructions)
                ilProcessor.Append(ins);
        }

    }
    internal class DefinitionCompleter
    {
        public DefinitionCompleter(ReferenceGenerator referenceGenerator)
        {
            this.Context = referenceGenerator.Context;
            this.ReferenceGenerator = referenceGenerator;
        }
        public Context Context { get; }
        public ReferenceGenerator ReferenceGenerator { get; }

        public void Complete(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            Cecil.TypeDefinition cecilTypeDefinition = Context.DefinitionMapping.TypesMap[typeDefinition];

            Cecil.TypeReference baseType = typeDefinition.Base == null ? null : ReferenceGenerator.TypeReference(typeDefinition.Base);
            cecilTypeDefinition.BaseType = baseType;

            AddConstraintsToGenericParameters(typeDefinition, cecilTypeDefinition);
            AddInterfaceImplementations(typeDefinition, cecilTypeDefinition);
        }

        public void Complete(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            Cecil.MethodDefinition cecilMethodDefinition = Context.DefinitionMapping.MethodsMap[methodDefinition];

            Cecil.TypeReference returnType = ReferenceGenerator.TypeReference(methodDefinition.ReturnType);
            cecilMethodDefinition.ReturnType = returnType;

            AddConstraintsToGenericParameters(methodDefinition, cecilMethodDefinition);

            Cecil.TypeDefinition containingType = ReferenceGenerator.TypeReference(methodDefinition.ContainingType).Resolve();
            cecilMethodDefinition.DeclaringType = containingType as Cecil.TypeDefinition;

            if (methodDefinition.HasBody)
            {
                cecilMethodDefinition.Body.MaxStackSize = methodDefinition.Body.MaxStack;
                IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions = CreateLocalVariables(methodDefinition, cecilMethodDefinition);
                IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions = CreateParametersWithBody(methodDefinition, cecilMethodDefinition);
                InstructionGenerator instructionGenerator = new InstructionGenerator(ReferenceGenerator);
                instructionGenerator.CreateInstructions(methodDefinition, cecilMethodDefinition, variableDefinitions, parameterDefinitions);
            }
            else
            {
                CreateParametersWithoutBody(methodDefinition, cecilMethodDefinition);
            }
        }

        private void CreateParametersWithoutBody(AnalysisNet.Types.MethodDefinition methodDefinition, Cecil.MethodDefinition methodDef)
        {
            foreach (var methodParameter in methodDefinition.Parameters)
            {
                if (methodParameter.Name.Equals("this"))
                    continue;

                var paramDef = new Cecil.ParameterDefinition(ReferenceGenerator.TypeReference(methodParameter.Type));
                if (methodParameter.DefaultValue != null)
                    paramDef.Constant = methodParameter.DefaultValue.Value;

                methodDef.Parameters.Add(paramDef);
            }
        }
        private IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> CreateParametersWithBody(AnalysisNet.Types.MethodDefinition methodDefinition, Mono.Cecil.MethodDefinition methodDef)
        {
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions = new Dictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition>();
            for (int i = 0; i < methodDefinition.Body.Parameters.Count; i++)
            {
                AnalysisNet.ThreeAddressCode.Values.IVariable localVariable = methodDefinition.Body.Parameters[i];
                if (localVariable.Name == "this")
                {
                    parameterDefinitions[localVariable] = methodDef.Body.ThisParameter;
                    continue;
                }

                var paramDef = new Cecil.ParameterDefinition(ReferenceGenerator.TypeReference(localVariable.Type));
                var extractedConstant = GetDefaultValueFromBodyParamIndex(methodDefinition, i);
                if (extractedConstant != null)
                    paramDef.Constant = extractedConstant;

                methodDef.Parameters.Add(paramDef);
                parameterDefinitions[localVariable] = paramDef;
            }
            return parameterDefinitions;
        }
        private Object GetDefaultValueFromBodyParamIndex(AnalysisNet.Types.MethodDefinition methodDefinition, int bodyParamIndex)
        {
            // body parameters contains 'this' while signature parameteres do not.

            int idx = bodyParamIndex;
            if (!methodDefinition.IsStatic)
                idx--;

            var defaultValue = methodDefinition.Parameters.ElementAt(idx).DefaultValue;

            if (defaultValue != null)
                return defaultValue.Value;

            return null;
        }

        private IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> CreateLocalVariables(Model.Types.MethodDefinition methodDefinition, Mono.Cecil.MethodDefinition methodDef)
        {
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions = new Dictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition>();

            foreach (AnalysisNet.ThreeAddressCode.Values.IVariable localVariable in methodDefinition.Body.LocalVariables)
            {
                var varDef = new Cecil.Cil.VariableDefinition(ReferenceGenerator.TypeReference(localVariable.Type));
                methodDef.Body.Variables.Add(varDef);
                variableDefinitions[localVariable] = varDef;
            }

            return variableDefinitions;
        }
        private void AddConstraintsToGenericParameters(AnalysisNet.Types.IGenericDefinition genericDefinition, Cecil.IGenericParameterProvider genericParameterProvider)
        {
            foreach (var analysisNetParameter in genericDefinition.GenericParameters)
            {
                var constraints = analysisNetParameter.Constraints.Select(c => new Cecil.GenericParameterConstraint(ReferenceGenerator.TypeReference(c)));
                genericParameterProvider.GenericParameters.ElementAt(analysisNetParameter.Index).Constraints.AddRange(constraints);
            }
        }

        private void AddInterfaceImplementations(AnalysisNet.Types.TypeDefinition typeDefinition, Cecil.TypeDefinition t)
        {
            foreach (var inter in typeDefinition.Interfaces)
                t.Interfaces.Add(new Cecil.InterfaceImplementation(ReferenceGenerator.TypeReference(inter)));
        }
    }
    internal class EmptyDefinitionGenerator
    {
        public EmptyDefinitionGenerator(Context context, ReferenceGenerator referenceGenerator)
        {
            this.Context = context;
            this.ReferenceGenerator = referenceGenerator;
        }
        public Cecil.TypeDefinition CreateEmptyTypeDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            if (typeDefinition.Kind == AnalysisNet.Types.TypeDefinitionKind.Struct)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition.Kind == AnalysisNet.Types.TypeDefinitionKind.Enum)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition.Kind == AnalysisNet.Types.TypeDefinitionKind.Interface)
            {
                return CreateInterfaceDefinition(typeDefinition);
            }
            else if (typeDefinition.Kind == AnalysisNet.Types.TypeDefinitionKind.Class)
            {
                return CreateClassDefinition(typeDefinition);
            }

            return null;
        }
        public Cecil.MethodDefinition CreateEmptyMethodDefinition(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            Cecil.MethodDefinition methodDef = new Mono.Cecil.MethodDefinition(methodDefinition.Name, GenerateMethodAttributes(methodDefinition), Context.CurrentModule.TypeSystem.Void);
            CreateGenericParameters(methodDefinition, methodDef);
            return methodDef;
        }
        private Cecil.TypeDefinition CreateClassDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            string namespaceName = typeDefinition.ContainingNamespace.FullName;
            Cecil.TypeAttributes attributes = Mono.Cecil.TypeAttributes.Class | GetVisibility(typeDefinition.Visibility);

            // hack: an abstract class can have no abstract methods
            // there is no field in the type definition
            if (typeDefinition.Methods.Any(m => m.IsAbstract))
                attributes |= Mono.Cecil.TypeAttributes.Abstract;

            var t = new Cecil.TypeDefinition(namespaceName, typeDefinition.MetadataName(), attributes);

            CreateGenericParameters(typeDefinition, t);

            return t;
        }

        private Cecil.TypeDefinition CreateInterfaceDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            var cecilDefinition = CreateClassDefinition(typeDefinition);
            cecilDefinition.Attributes |= Cecil.TypeAttributes.Interface;
            cecilDefinition.Attributes |= Cecil.TypeAttributes.Abstract;
            // todo: not sure about this
            cecilDefinition.Attributes &= ~Cecil.TypeAttributes.Class;
            return cecilDefinition;
        }

        private void CreateGenericParameters(AnalysisNet.Types.IGenericDefinition genericDefinition, Cecil.IGenericParameterProvider genericParameterProvider)
        {
            foreach (var gp in Enumerable.Repeat(new Cecil.GenericParameter(genericParameterProvider), genericDefinition.GenericParameters.Count))
                genericParameterProvider.GenericParameters.Add(gp);
        }
        private Cecil.TypeAttributes GetVisibility(AnalysisNet.Types.VisibilityKind visibility)
        {
            return Cecil.TypeAttributes.Public;
        }
        private Cecil.MethodAttributes GenerateMethodAttributes(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            // readme: methods defined in interfaces are flagged as external (cci does it)
            // even if the assembly in which they are defined is loaded. The same happens with abstract methods.
            if (methodDefinition.IsExternal &&
                methodDefinition.ContainingType.Kind != Model.Types.TypeDefinitionKind.Interface && !methodDefinition.IsAbstract)
            {
                throw new NotImplementedException();
            }

            Cecil.MethodAttributes res = GetMethodVisibility();

            if (methodDefinition.IsStatic)
                res |= Cecil.MethodAttributes.Static;

            if (methodDefinition.IsAbstract)
                res |= Cecil.MethodAttributes.Abstract;

            if (methodDefinition.IsVirtual)
                res |= Cecil.MethodAttributes.Virtual;

            if (methodDefinition.IsConstructor)
            {
                res |= Cecil.MethodAttributes.HideBySig;
                res |= Cecil.MethodAttributes.SpecialName;
                res |= Cecil.MethodAttributes.RTSpecialName;
            }

            return res;
        }

        private Cecil.MethodAttributes GetMethodVisibility()
        {
            return Cecil.MethodAttributes.Public;
        }

        public Context Context { get; }
        public ReferenceGenerator ReferenceGenerator { get; private set; }
    }
}