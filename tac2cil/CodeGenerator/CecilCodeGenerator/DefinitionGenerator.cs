using System;
using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;
namespace CodeGenerator.CecilCodeGenerator
{
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
    internal abstract class DefinitionGenerator
    {
        public DefinitionGenerator(ReferenceGenerator referenceGenerator)
        {
            this.Context = referenceGenerator.Context;
            this.ReferenceGenerator = referenceGenerator;
        }

        public Context Context { get; }
        public ReferenceGenerator ReferenceGenerator { get; }
        protected void AddConstraintsToGenericParameters(AnalysisNet.Types.IGenericDefinition genericDefinition, Cecil.IGenericParameterProvider genericParameterProvider)
        {
            foreach (var analysisNetParameter in genericDefinition.GenericParameters)
            {
                var constraints = analysisNetParameter.Constraints.Select(c => new Cecil.GenericParameterConstraint(ReferenceGenerator.TypeReference(c)));
                genericParameterProvider.GenericParameters.ElementAt(analysisNetParameter.Index).Constraints.AddRange(constraints);
            }
        }
    }

    internal class MethodGenerator : DefinitionGenerator
    {
        public MethodGenerator(ReferenceGenerator referenceGenerator) : base(referenceGenerator) { }

        public Cecil.MethodDefinition MethodDefinition(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            Cecil.MethodDefinition cecilMethodDefinition = new Cecil.MethodDefinition(methodDefinition.Name, 0, Context.CurrentModule.TypeSystem.Void);
            GenerateMethodAttributes(methodDefinition, cecilMethodDefinition);
            cecilMethodDefinition.CreateGenericParameters(methodDefinition.GenericParameters.Count);
            
            Cecil.TypeReference returnType = ReferenceGenerator.TypeReference(methodDefinition.ReturnType);
            cecilMethodDefinition.ReturnType = returnType;
            AddConstraintsToGenericParameters(methodDefinition, cecilMethodDefinition);

            var typeRef = ReferenceGenerator.TypeReference(methodDefinition.ContainingType);
            Cecil.TypeDefinition containingType = typeRef.Resolve();
            cecilMethodDefinition.DeclaringType = containingType as Cecil.TypeDefinition;

            SetOverrides(methodDefinition, cecilMethodDefinition);

            if (methodDefinition.HasBody)
            {
                cecilMethodDefinition.Body.MaxStackSize = methodDefinition.Body.MaxStack;
                cecilMethodDefinition.Body.InitLocals = methodDefinition.Body.LocalVariables.Count > 0;
                IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions = CreateLocalVariables(methodDefinition, cecilMethodDefinition);
                IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions = CreateParametersWithBody(methodDefinition, cecilMethodDefinition);
                InstructionGenerator instructionGenerator = new InstructionGenerator(ReferenceGenerator);
                instructionGenerator.CreateInstructions(methodDefinition, cecilMethodDefinition, variableDefinitions, parameterDefinitions);
            }
            else
            {
                CreateParametersWithoutBody(methodDefinition, cecilMethodDefinition);
            }

            return cecilMethodDefinition;
        }

        private void SetOverrides(AnalysisNet.Types.MethodDefinition methodDefinition, Cecil.MethodDefinition methodDef)
        {
            var impls = methodDefinition.ContainingType.ExplicitOverrides;
            var matchedImpls = impls.Where(impl => methodDefinition.MatchReference(impl.ImplementingMethod));
            methodDef.Overrides.AddRange(matchedImpls.Select(impl => ReferenceGenerator.MethodReference(impl.ImplementedMethod)));
        }

        private void CreateParametersWithoutBody(AnalysisNet.Types.MethodDefinition methodDefinition, Cecil.MethodDefinition methodDef)
        {
            foreach (var methodParameter in methodDefinition.Parameters)
            {
                if (methodParameter.Name.Equals("this"))
                    continue;

                var paramDef = new Cecil.ParameterDefinition(ReferenceGenerator.TypeReference(methodParameter.Type));
                if (methodParameter.DefaultValue != null)
                {
                    paramDef.Constant = methodParameter.DefaultValue.Value;
                    paramDef.HasDefault = true;
                }

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
                {
                    paramDef.Constant = extractedConstant;
                    paramDef.HasDefault = true;
                }

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


        private Cecil.MethodAttributes GetVisibility(AnalysisNet.Types.VisibilityKind visibility)
        {
            return Cecil.MethodAttributes.Public;
        }

        private void GenerateMethodAttributes(AnalysisNet.Types.MethodDefinition methodDefinition, Cecil.MethodDefinition cecilMethodDefinition)
        {
            cecilMethodDefinition.IsPublic = true;

            if (methodDefinition.IsStatic)
            {
                cecilMethodDefinition.IsStatic = true;
                cecilMethodDefinition.HasThis = false;
            }
            else
                cecilMethodDefinition.HasThis = true;

            if (methodDefinition.IsAbstract)
                cecilMethodDefinition.IsAbstract = true;

            if (methodDefinition.IsVirtual)
            {
                cecilMethodDefinition.IsVirtual = true;
                cecilMethodDefinition.IsHideBySig = true;

                if (methodDefinition.IsOverrider)
                    cecilMethodDefinition.IsReuseSlot = true;
                else
                    cecilMethodDefinition.IsNewSlot = true;
            }

            if (methodDefinition.IsFinal)
                cecilMethodDefinition.IsFinal = true;

            if (methodDefinition.IsConstructor)
            {
                cecilMethodDefinition.IsHideBySig = true;
                cecilMethodDefinition.IsSpecialName = true;
                cecilMethodDefinition.IsRuntimeSpecialName = true;
            }

            if (methodDefinition.ContainingType.Kind == AnalysisNet.Types.TypeDefinitionKind.Delegate)
                cecilMethodDefinition.IsRuntime = true;
        }
    }
    internal class TypeGenerator : DefinitionGenerator
    {
        public TypeGenerator(ReferenceGenerator referenceGenerator) : base(referenceGenerator)
        {
        }
        private Cecil.TypeAttributes GetVisibility(AnalysisNet.Types.VisibilityKind visibility)
        {
            return Cecil.TypeAttributes.Public;
        }

        public Cecil.TypeDefinition TypeDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
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
            else if (typeDefinition.Kind == AnalysisNet.Types.TypeDefinitionKind.Delegate)
            {
                return CreateDelegateDefinition(typeDefinition);
            }

            throw new NotImplementedException();
        }

        private Cecil.TypeDefinition CreateDelegateDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            var definition = CreateClassDefinition(typeDefinition);
            definition.IsSealed = true;
            return definition;
        }
        private void SetAttributes(AnalysisNet.Types.TypeDefinition typeDefinition, Cecil.TypeDefinition cecilDef)
        {
            // hack: an abstract class can have no abstract methods
            // there is no field in the type definition
            if (typeDefinition.Methods.Any(m => m.IsAbstract))
                cecilDef.IsAbstract = true;

            if (typeDefinition.ContainingType != null)
                cecilDef.IsNestedPublic = true;
            else
                cecilDef.IsPublic = true;
        }
        private void SetDeclaringType(AnalysisNet.Types.TypeDefinition typeDefinition, Cecil.TypeDefinition cecilDef)
        {
            Cecil.TypeReference declaringTypeRef = typeDefinition.ContainingType == null ? null : ReferenceGenerator.TypeReference(typeDefinition.ContainingType);
            if (declaringTypeRef != null)
            {
                Cecil.TypeDefinition declaringType = declaringTypeRef.Resolve();
                declaringType.NestedTypes.Add(cecilDef);
                cecilDef.DeclaringType = declaringType;
            }
        }
        private void SetBaseType(AnalysisNet.Types.TypeDefinition typeDefinition, Cecil.TypeDefinition cecilDef)
        {
            Cecil.TypeReference baseType = typeDefinition.Base == null ? null : ReferenceGenerator.TypeReference(typeDefinition.Base);
            cecilDef.BaseType = baseType;
        }
        private Cecil.TypeDefinition CreateClassDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            string namespaceName = typeDefinition.ContainingNamespace.FullName;
            string name = typeDefinition.MetadataName();

            var t = new Cecil.TypeDefinition(namespaceName, name, Cecil.TypeAttributes.Class);

            SetAttributes(typeDefinition, t);
            t.CreateGenericParameters(typeDefinition.GenericParameters.Count);

            SetBaseType(typeDefinition, t);
            SetDeclaringType(typeDefinition, t);

            AddConstraintsToGenericParameters(typeDefinition, t);
            AddInterfaceImplementations(typeDefinition, t);
            CreateFieldDefinitions(typeDefinition, t);
            return t;
        }

        private void AddInterfaceImplementations(AnalysisNet.Types.TypeDefinition typeDefinition, Cecil.TypeDefinition t)
        {
            foreach (var inter in typeDefinition.Interfaces)
                t.Interfaces.Add(new Cecil.InterfaceImplementation(ReferenceGenerator.TypeReference(inter)));
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

        private void CreateFieldDefinitions(AnalysisNet.Types.TypeDefinition analysisNetDef, Cecil.TypeDefinition cecilDef)
        {
            foreach (var field in analysisNetDef.Fields)
                cecilDef.Fields.Add(CreateFieldDefinition(field));
        }

        private Cecil.FieldDefinition CreateFieldDefinition(AnalysisNet.Types.FieldDefinition fieldDefinition)
        {
            var fieldAttribute = Cecil.FieldAttributes.Public;
            if (fieldDefinition.IsStatic)
                fieldAttribute |= Cecil.FieldAttributes.Static;

            Cecil.TypeReference fieldType = ReferenceGenerator.TypeReference(fieldDefinition.Type);
            Cecil.FieldDefinition cecilField = new Mono.Cecil.FieldDefinition(fieldDefinition.Name, fieldAttribute, fieldType);

            return cecilField;
        }
    }
}

