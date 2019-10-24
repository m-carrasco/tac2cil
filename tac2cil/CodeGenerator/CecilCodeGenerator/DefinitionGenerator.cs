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

        public IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> CreateInstructions(Model.Types.MethodDefinition methodDefinition,
            Mono.Cecil.MethodDefinition methodDef,
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions,
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions)
        {
            Cecil.Cil.ILProcessor ilProcessor = methodDef.Body.GetILProcessor();
            BytecodeTranslator translator = new BytecodeTranslator(methodDefinition, variableDefinitions, parameterDefinitions, ReferenceGenerator, ilProcessor);

            // analysis net instruction -> [cecil instructions]
            var mappingTranslatedInstructions = translator.Translate();

            var instructions = mappingTranslatedInstructions.Values.SelectMany(l => l);
            foreach (Mono.Cecil.Cil.Instruction ins in instructions)
                ilProcessor.Append(ins);

            return mappingTranslatedInstructions;
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

        private void SetCustomAttributes(AnalysisNet.Types.MethodDefinition methodDefinition, Cecil.MethodDefinition methodDef)
        {
            foreach (var analysisNetAttr in methodDefinition.Attributes)
            {
                var ctor = ReferenceGenerator.MethodReference(analysisNetAttr.Constructor);
                var type = ReferenceGenerator.TypeReference(analysisNetAttr.Type);

                var cecilAttr = new Cecil.CustomAttribute(ctor);
                foreach (var constant in analysisNetAttr.Arguments)
                {
                    // todo: cci is not working correctly
                    if (constant == null)
                        continue;

                    var cecilArg = new Cecil.CustomAttributeArgument(ReferenceGenerator.TypeReference(constant.Type), constant.Value);
                    cecilAttr.ConstructorArguments.Add(cecilArg);
                }

                methodDef.CustomAttributes.Add(cecilAttr);
            }
        }
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
            SetCustomAttributes(methodDefinition, cecilMethodDefinition);
            var parameterDefinitions = CreateParameters(methodDefinition, cecilMethodDefinition);

            if (methodDefinition.HasBody)
            {
                cecilMethodDefinition.Body.MaxStackSize = methodDefinition.Body.MaxStack;
                cecilMethodDefinition.Body.InitLocals = methodDefinition.Body.LocalVariables.Count > 0;
                IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions = CreateLocalVariables(methodDefinition, cecilMethodDefinition);
                InstructionGenerator instructionGenerator = new InstructionGenerator(ReferenceGenerator);
                
                // analysis-net instruction -> [cecil instruction]
                var mapInstructions = instructionGenerator.CreateInstructions(methodDefinition, cecilMethodDefinition, variableDefinitions, parameterDefinitions);

                CreateExceptionHandlers(mapInstructions, methodDefinition.Body, cecilMethodDefinition.Body);
            }

            return cecilMethodDefinition;
        }

        private void SetOverrides(AnalysisNet.Types.MethodDefinition methodDefinition, Cecil.MethodDefinition methodDef)
        {
            var impls = methodDefinition.ContainingType.ExplicitOverrides;
            var matchedImpls = impls.Where(impl => methodDefinition.MatchReference(impl.ImplementingMethod));
            methodDef.Overrides.AddRange(matchedImpls.Select(impl => ReferenceGenerator.MethodReference(impl.ImplementedMethod)));
        }

        private IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> CreateParameters(AnalysisNet.Types.MethodDefinition methodDefinition, Mono.Cecil.MethodDefinition methodDef)
        {
            IDictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions = new Dictionary<AnalysisNet.ThreeAddressCode.Values.IVariable, Cecil.ParameterDefinition>();

            for (int idx = 0; idx < methodDefinition.Parameters.Count; idx++)
            {
                var methodParameter = methodDefinition.Parameters.ElementAt(idx);
                //if (methodParameter.Name.Equals("this"))
                //    continue;

                var paramDef = new Cecil.ParameterDefinition(ReferenceGenerator.TypeReference(methodParameter.Type));
                if (methodParameter.DefaultValue != null)
                {
                    paramDef.Constant = methodParameter.DefaultValue.Value;
                    paramDef.HasDefault = true;
                }

                if (methodParameter.Kind == AnalysisNet.Types.MethodParameterKind.In)
                    paramDef.IsIn = true;
                else if (methodParameter.Kind == AnalysisNet.Types.MethodParameterKind.Out)
                    paramDef.IsOut = true;

                methodDef.Parameters.Add(paramDef);

                // map body parameters to cecil parameters
                if (methodDefinition.HasBody && methodDefinition.Body.Parameters.Count > 0) {

                    // body parameters contain 'this' while analysis-net's parameters do not
                    int localIdx = (methodDefinition.IsStatic ? 0 : 1) + idx;
                    var localVariable = methodDefinition.Body.Parameters.ElementAt(localIdx);
                    parameterDefinitions[localVariable] = paramDef;
                }
            }

            return parameterDefinitions;
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

            // analysis-net does not flag static constructors
            bool isStaticCtor = methodDefinition.Name.Equals(".cctor") && methodDefinition.IsStatic;
            if (methodDefinition.IsConstructor || isStaticCtor)
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
                return CreateStructDefinition(typeDefinition);
            }
            else if (typeDefinition.Kind == AnalysisNet.Types.TypeDefinitionKind.Enum)
            {
                return CreateEnumDefinition(typeDefinition);
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

        private Cecil.TypeDefinition CreateEnumDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            var def = CreateClassDefinition(typeDefinition);
            def.IsSealed = true;
            foreach (var field in def.Fields)
            {
                field.IsStatic = true;
                field.IsLiteral = true;
                field.HasDefault = true;
            }

            var underlyingType = ReferenceGenerator.TypeReference(typeDefinition.UnderlayingType);
            var value__ = new Cecil.FieldDefinition("value__", Cecil.FieldAttributes.RTSpecialName | Cecil.FieldAttributes.SpecialName, underlyingType);
            def.Fields.Insert(0,value__);

            return def;
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

            // if TypeDef is a Value Type, Enum or Delegate must be Sealed.
            if (typeDefinition.Base != null && 
                typeDefinition.Base.Equals(AnalysisNet.Types.PlatformTypes.ValueType))
                cecilDef.IsSealed = true;
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
            string namespaceName = typeDefinition.ContainingType != null ? String.Empty : typeDefinition.ContainingNamespace.FullName;
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
        private Cecil.TypeDefinition CreateStructDefinition(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            var cecilDefinition = CreateClassDefinition(typeDefinition);
            cecilDefinition.IsSequentialLayout = true;
            cecilDefinition.IsSealed = true;
            return cecilDefinition;
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
            Cecil.FieldDefinition cecilField = new Cecil.FieldDefinition(fieldDefinition.Name, fieldAttribute, fieldType);
            
            if (fieldDefinition.Value != null)
            {
                cecilField.Constant = fieldDefinition.Value.Value;
                cecilField.HasConstant = true;
            }

            return cecilField;
        }
    }
}

