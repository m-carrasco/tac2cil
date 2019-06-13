using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Model;
using Model.ThreeAddressCode.Values;
using Model.Types;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace CodeGenerator
{
    public class CodeGenerator : ICodeGenerator
    {
        private readonly Model.Host host;
        public CodeGenerator(Model.Host h)
        {
            host = h;
        }

        public Model.Host Host
        {
            get { return host; }
        }

        private IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap = 
            new Dictionary<Model.Assembly, AssemblyDefinition>();
        
        public void GenerateAssemblies(string pathToFolder)
        {
            foreach (var analysisNetAssembly in host.Assemblies)
            {
                string moduleName = analysisNetAssembly.Name;
                ModuleKind moduleKind = ModuleKind.Dll;

                AssemblyDefinition cecilAssembly = AssemblyDefinition.CreateAssembly(
                    new AssemblyNameDefinition(analysisNetAssembly.Name, new Version(1, 0, 0, 0)), moduleName, moduleKind);

                assembliesMap[analysisNetAssembly] = cecilAssembly;
            }

            foreach (var keyval in assembliesMap)
            {
                var cecilAssembly = keyval.Value;
                var analysisNetAssembly = keyval.Key;

                ModuleDefinition module = cecilAssembly.MainModule;

                TypeReferenceGenerator typeReferenceGenerator = new TypeReferenceGenerator(module, assembliesMap, host);

                foreach (var analysisNetType in analysisNetAssembly.RootNamespace.GetAllTypes())
                {
                    TypeDefinitionGenerator typeDefGen = new TypeDefinitionGenerator(analysisNetType, module, typeReferenceGenerator);
                    module.Types.Add(typeDefGen.Generate());
                }
            }

            foreach (var assembly in assembliesMap.Values)
            {
                assembly.Write(Path.Combine(pathToFolder, assembly.Name.Name));
            }
        }
    }
    class TypeDefinitionGenerator
    {
        private ITypeDefinition def;
        private ModuleDefinition module;
        private TypeReferenceGenerator typeReferenceGenerator;

        public TypeDefinitionGenerator(Model.Types.ITypeDefinition def, ModuleDefinition module, TypeReferenceGenerator typeReferenceGenerator)
        {
            this.def = def;
            this.module = module;
            this.typeReferenceGenerator = typeReferenceGenerator;
        }

        public TypeDefinition Generate()
        {
            ITypeDefinition typeDefinition = def;

            if (typeDefinition is Model.Types.StructDefinition structDef)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition is Model.Types.EnumDefinition enumDef)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition is Model.Types.InterfaceDefinition interfaceDef)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition is Model.Types.ClassDefinition typeDef)
            {
                string namespaceName = typeDef.ContainingNamespace.ContainingNamespace.Name;
                var t = new TypeDefinition(namespaceName, typeDef.Name,
                    Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, typeReferenceGenerator.GenerateTypeReference(typeDef.Base));

                foreach (var methodDefinition in typeDef.Methods)
                {
                    MethodDefinitionGenerator methodDefinitionGen = new MethodDefinitionGenerator(methodDefinition, typeReferenceGenerator);
                    t.Methods.Add(methodDefinitionGen.GenerateMethodDefinition());
                }

                foreach (var inter in typeDef.Interfaces)
                    t.Interfaces.Add(new InterfaceImplementation(typeReferenceGenerator.GenerateTypeReference(inter)));

                return t;
            }

            throw new NotImplementedException();
        }
    }

    class TypeReferenceGenerator
    {
        private ModuleDefinition currentModule;
        private IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap;
        private Host host;
        public TypeReferenceGenerator(ModuleDefinition moduleDefinition, IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap, Host host)
        {
            this.currentModule = moduleDefinition;
            this.assembliesMap = assembliesMap;
            this.host = host;
        }

        public TypeReference GenerateTypeReference(Model.Types.IBasicType basicType)
        {
            if (basicType.Equals(Model.Types.PlatformTypes.Object))
                return currentModule.TypeSystem.Object;

            if (basicType.Equals(Model.Types.PlatformTypes.Void))
                return currentModule.TypeSystem.Void;

            if (basicType.Equals(Model.Types.PlatformTypes.Int32))
                return currentModule.TypeSystem.Int32;

            ModuleDefinition moduleDefinition = basicType is Model.Types.ITypeDefinition typeDef ? ModuleDefinitionForTypeDefinition(typeDef) : null;
            IMetadataScope metadataScope = null;

            TypeReference typeReference = new TypeReference(basicType.ContainingNamespace, basicType.Name, moduleDefinition, metadataScope);

            return typeReference;
        }

        public TypeReference GenerateTypeReference(Model.Types.IType type)
        {
            if (type is IBasicType basicType)
            {
                //StructDefinition
                //EnumDefinition
                //InterfaceDefinition
                //ClassDefinition 
                //BasicType

                return GenerateTypeReference(basicType);
            }

            if (type is GenericParameterReference genericParameterReference)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.GenericParameter genericParameter)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.FunctionPointerType functionPointerType)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.PointerType pointerType)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.ArrayType arrayType)
            {
                throw new NotImplementedException();
            }

            if (type is UnknownType unknownType)
            {
                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        private ModuleDefinition ModuleDefinitionForTypeDefinition(ITypeDefinition typeDefinition)
        {
            // the type definition must be in some of the loaded assemblies
            // we are going to look for its containing module

            var containingAssembly = host.ResolveReference(typeDefinition.ContainingAssembly);
            Contract.Assert(containingAssembly != null);

            ModuleDefinition moduleDef = assembliesMap[containingAssembly].MainModule;
            Contract.Assert(moduleDef != null);

            return moduleDef;
        }
    }

    class MethodDefinitionGenerator
    {
        private Model.Types.MethodDefinition methodDefinition;
        private TypeReferenceGenerator typeReferenceGenerator;

        public MethodDefinitionGenerator(Model.Types.MethodDefinition methodDefinition, TypeReferenceGenerator typeReferenceGenerator)
        {
            this.methodDefinition = methodDefinition;
            this.typeReferenceGenerator = typeReferenceGenerator;

            Contract.Assert(methodDefinition != null);
            Contract.Assert(methodDefinition.Body.Kind == MethodBodyKind.Bytecode);
        }

        private Mono.Cecil.MethodAttributes GenerateMethodAttributes()
        {
            if (methodDefinition.IsExternal)
            {
                // i dont know what it should be in mono
                // maybe this does not correspond to a mono cecil attribute
                throw new NotImplementedException();
            }

            // for now we are forcing this!
            MethodAttributes res = MethodAttributes.Public;

            if (methodDefinition.IsStatic)
                res |= MethodAttributes.Static;

            if (methodDefinition.IsAbstract)
                res |= MethodAttributes.Abstract;

            if (methodDefinition.IsVirtual)
                res |= MethodAttributes.Virtual;

            return res;
        }

        public Mono.Cecil.MethodDefinition GenerateMethodDefinition()
        {
            Mono.Cecil.MethodDefinition methodDef = new Mono.Cecil.MethodDefinition(methodDefinition.Name, GenerateMethodAttributes(), typeReferenceGenerator.GenerateTypeReference(methodDefinition.ReturnType));

            methodDef.Body.MaxStackSize = methodDefinition.Body.MaxStack;

            int idx = 0;
            IDictionary<IVariable, VariableDefinition> variableDefinitions = new Dictionary<IVariable, VariableDefinition>();
            foreach (IVariable localVariable in methodDefinition.Body.LocalVariables.Where(v=> !v.IsParameter))
            {
                if (localVariable.Type is Model.Types.PointerType)
                    throw new NotImplementedException(); // we should specify if it is in/ref?
                var varDef = new VariableDefinition(typeReferenceGenerator.GenerateTypeReference(localVariable.Type));
                methodDef.Body.Variables.Add(varDef);
                variableDefinitions[localVariable] = varDef;
                idx++;
            }

            idx = 0;
            IDictionary<IVariable, ParameterDefinition> parameterDefinitions = new Dictionary<IVariable, ParameterDefinition>();
            foreach (IVariable localVariable in methodDefinition.Body.LocalVariables.Where(v => v.IsParameter))
            {
                if (localVariable.Type is Model.Types.PointerType)
                    throw new NotImplementedException(); // we should specify if it is in/ref?
                var paramDef = new ParameterDefinition(typeReferenceGenerator.GenerateTypeReference(localVariable.Type));
                methodDef.Parameters.Add(paramDef);
                parameterDefinitions[localVariable] = paramDef;
            }

            BytecodeTranslator translator = new BytecodeTranslator(methodDef, variableDefinitions, parameterDefinitions);
            translator.Visit(methodDefinition.Body);

            return methodDef;
        }

        private class BytecodeTranslator : Model.Bytecode.Visitor.InstructionVisitor
        {
            private Mono.Cecil.MethodDefinition methodDefinition;
            private ILProcessor processor = null;
            private IDictionary<IVariable, VariableDefinition> variableDefinitions;
            private IDictionary<IVariable, ParameterDefinition> parameterDefinitions;

            public BytecodeTranslator(Mono.Cecil.MethodDefinition methodDefinition,
                IDictionary<IVariable, VariableDefinition> variableDefinitions, 
                IDictionary<IVariable, ParameterDefinition> parameterDefinitions)
            {
                this.methodDefinition = methodDefinition;
                this.processor = methodDefinition.Body.GetILProcessor();
                this.variableDefinitions = variableDefinitions;
                this.parameterDefinitions = parameterDefinitions;
            }
            
        public override void Visit(Model.Bytecode.BasicInstruction instruction)
            {
                Nullable<Mono.Cecil.Cil.OpCode> op = null;
                switch (instruction.Operation)
                {
                    case Model.Bytecode.BasicOperation.Add:
                        if (instruction.OverflowCheck && instruction.UnsignedOperands)
                        {
                            op = Mono.Cecil.Cil.OpCodes.Add_Ovf_Un;
                        } else if (instruction.OverflowCheck && !instruction.UnsignedOperands)
                        {
                            op = Mono.Cecil.Cil.OpCodes.Add_Ovf;
                        } else if (!instruction.OverflowCheck && instruction.UnsignedOperands)
                        {
                            op = Mono.Cecil.Cil.OpCodes.Add;
                        } else if (!instruction.OverflowCheck && !instruction.UnsignedOperands)
                        {
                            op = Mono.Cecil.Cil.OpCodes.Add;
                        }
                        break;
                    case Model.Bytecode.BasicOperation.Nop:
                        op = Mono.Cecil.Cil.OpCodes.Nop;
                        break;
                    case Model.Bytecode.BasicOperation.Pop:
                        op = Mono.Cecil.Cil.OpCodes.Pop;
                        break;
                    case Model.Bytecode.BasicOperation.Return:
                        op = Mono.Cecil.Cil.OpCodes.Ret;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                processor.Emit(op.Value);
            }

            public override void Visit(Model.Bytecode.LoadInstruction instruction)
            {
                if (instruction.Operation == Model.Bytecode.LoadOperation.Content)
                {
                    Contract.Assert(instruction.Operand is IVariable);
                    IVariable variable = instruction.Operand as IVariable;

                    if (variable.IsParameter)
                        processor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, parameterDefinitions[variable]);
                    else
                        processor.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, variableDefinitions[variable]);

                } else if (instruction.Operation == Model.Bytecode.LoadOperation.Value)
                {
                    if (instruction.Operand is Constant constant)
                    {
                        if (constant.Value is sbyte asSbyte)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asSbyte);
                        }
                        else if (constant.Value is byte asByte)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asByte);
                        }
                        else if (constant.Value is int asInt)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asInt);
                        }
                        else if (constant.Value is long asLong)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asLong);
                        }
                        else if (constant.Value is float asFloat)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asFloat);
                        }
                        else if (constant.Value is double asDouble)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asDouble);
                        } else if (constant.Value is string asString)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asString);
                        }
                        else
                            throw new NotImplementedException();

                    } else if (instruction.Operand is IVariable variable)
                    {
                        throw new NotImplementedException();
                    } else if (instruction.Operand is UnknownValue unk)
                    {
                        throw new NotImplementedException();
                    } else
                        throw new NotImplementedException();

                } else
                    throw new NotImplementedException();
            }

            public override void Visit(Model.Bytecode.LoadFieldInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.LoadMethodAddressInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.StoreInstruction instruction)
            {
                if (instruction.Target.IsParameter)
                    processor.Emit(Mono.Cecil.Cil.OpCodes.Starg, parameterDefinitions[instruction.Target]);
                else
                    processor.Emit(Mono.Cecil.Cil.OpCodes.Stloc, variableDefinitions[instruction.Target]);
            }

            public override void Visit(Model.Bytecode.StoreFieldInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.ConvertInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.BranchInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.SwitchInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.SizeofInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.LoadTokenInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.MethodCallInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.IndirectMethodCallInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.CreateObjectInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.CreateArrayInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.LoadArrayElementInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.StoreArrayElementInstruction instruction) { throw new NotImplementedException(); }

        }
    }
}
