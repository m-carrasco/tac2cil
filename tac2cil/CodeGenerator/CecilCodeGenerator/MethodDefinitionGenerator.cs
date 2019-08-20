using Model.ThreeAddressCode.Values;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace CodeGenerator.CecilCodeGenerator
{
    class MethodDefinitionGenerator
    {
        private Model.Types.MethodDefinition methodDefinition;
        private TypeReferenceGenerator typeReferenceGenerator;
        private TypeDefinition containingType;

        public MethodDefinitionGenerator(Model.Types.MethodDefinition methodDefinition, TypeReferenceGenerator typeReferenceGenerator, TypeDefinition containingType)
        {
            this.methodDefinition = methodDefinition;
            this.typeReferenceGenerator = typeReferenceGenerator;
            this.containingType = containingType;

            Contract.Assert(typeReferenceGenerator != null);
            Contract.Assert(containingType != null);
            Contract.Assert(methodDefinition != null);
            Contract.Assert(methodDefinition.Body.Kind == Model.Types.MethodBodyKind.Bytecode);
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

            if (methodDefinition.IsConstructor)
            {
                res |= MethodAttributes.HideBySig;
                res |= MethodAttributes.SpecialName;
                res |= MethodAttributes.RTSpecialName;
            }

            return res;
        }

        public Mono.Cecil.MethodDefinition GenerateMethodDefinition()
        {
            Mono.Cecil.MethodDefinition methodDef = new Mono.Cecil.MethodDefinition(methodDefinition.Name, GenerateMethodAttributes(), typeReferenceGenerator.GenerateTypeReference(methodDefinition.ReturnType));
            methodDef.DeclaringType = containingType;

            methodDef.Body.MaxStackSize = methodDefinition.Body.MaxStack;
            IDictionary<IVariable, VariableDefinition> variableDefinitions = new Dictionary<IVariable, VariableDefinition>();
            foreach (IVariable localVariable in methodDefinition.Body.LocalVariables)
            {
                if (localVariable.Type is Model.Types.PointerType)
                    throw new NotImplementedException(); // we should specify if it is in/ref?
                var varDef = new VariableDefinition(typeReferenceGenerator.GenerateTypeReference(localVariable.Type));
                methodDef.Body.Variables.Add(varDef);
                variableDefinitions[localVariable] = varDef;
            }

            IDictionary<IVariable, ParameterDefinition> parameterDefinitions = new Dictionary<IVariable, ParameterDefinition>();
            foreach (IVariable localVariable in methodDefinition.Body.Parameters)
            {
                if (localVariable.Type is Model.Types.PointerType)
                    throw new NotImplementedException(); // we should specify if it is in/ref?

                if (localVariable.Name == "this")
                {
                    parameterDefinitions[localVariable] = methodDef.Body.ThisParameter;
                    continue;
                }

                var paramDef = new ParameterDefinition(typeReferenceGenerator.GenerateTypeReference(localVariable.Type));
                methodDef.Parameters.Add(paramDef);
                parameterDefinitions[localVariable] = paramDef;
            }
            BytecodeTranslator translator = new BytecodeTranslator(methodDef, variableDefinitions, parameterDefinitions, typeReferenceGenerator);
            translator.Visit(methodDefinition.Body);

            return methodDef;
        }

        private class BytecodeTranslator : Model.Bytecode.Visitor.InstructionVisitor
        {
            private Mono.Cecil.MethodDefinition methodDefinition;
            private ILProcessor processor = null;
            private IDictionary<IVariable, VariableDefinition> variableDefinitions;
            private IDictionary<IVariable, ParameterDefinition> parameterDefinitions;
            private TypeReferenceGenerator typeReferenceGenerator;

            public BytecodeTranslator(Mono.Cecil.MethodDefinition methodDefinition,
                IDictionary<IVariable, VariableDefinition> variableDefinitions,
                IDictionary<IVariable, ParameterDefinition> parameterDefinitions,
                TypeReferenceGenerator typeReferenceGenerator)
            {
                this.methodDefinition = methodDefinition;
                this.processor = methodDefinition.Body.GetILProcessor();
                this.variableDefinitions = variableDefinitions;
                this.parameterDefinitions = parameterDefinitions;
                this.typeReferenceGenerator = typeReferenceGenerator;
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
                        }
                        else if (instruction.OverflowCheck && !instruction.UnsignedOperands)
                        {
                            op = Mono.Cecil.Cil.OpCodes.Add_Ovf;
                        }
                        else if (!instruction.OverflowCheck && instruction.UnsignedOperands)
                        {
                            op = Mono.Cecil.Cil.OpCodes.Add;
                        }
                        else if (!instruction.OverflowCheck && !instruction.UnsignedOperands)
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
                    {
                        //if (variable.Name != "this")
                       //     processor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, parameterDefinitions[variable]);
                        //else
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg, 0);
                    }
                    else
                        processor.Emit(Mono.Cecil.Cil.OpCodes.Ldloc, variableDefinitions[variable]);

                }
                else if (instruction.Operation == Model.Bytecode.LoadOperation.Value)
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
                        }
                        else if (constant.Value is string asString)
                        {
                            processor.Emit(Mono.Cecil.Cil.OpCodes.Ldc_I4, asString);
                        }
                        else
                            throw new NotImplementedException();

                    }
                    else if (instruction.Operand is IVariable variable)
                    {
                        throw new NotImplementedException();
                    }
                    else if (instruction.Operand is UnknownValue unk)
                    {
                        throw new NotImplementedException();
                    }
                    else
                        throw new NotImplementedException();

                }
                else
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
            public override void Visit(Model.Bytecode.MethodCallInstruction instruction)
            {
                var methodReference = new MethodReference(instruction.Method.Name,
                    typeReferenceGenerator.GenerateTypeReference(instruction.Method.ReturnType),
                    typeReferenceGenerator.GenerateTypeReference(instruction.Method.ContainingType));

                //methodReference = typeReferenceGenerator.GenerteMethodReference(methodReference);

                if (instruction.Operation == Model.Bytecode.MethodCallOperation.Static)
                    processor.Emit(Mono.Cecil.Cil.OpCodes.Call, methodReference);
                else
                    throw new NotImplementedException();
            }
            public override void Visit(Model.Bytecode.IndirectMethodCallInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.CreateObjectInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.CreateArrayInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.LoadArrayElementInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.StoreArrayElementInstruction instruction) { throw new NotImplementedException(); }

        }
    }
}
