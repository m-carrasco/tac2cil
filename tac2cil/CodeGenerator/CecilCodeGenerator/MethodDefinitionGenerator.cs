using Model.Bytecode;
using Model.ThreeAddressCode.Values;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
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
        private ModuleDefinition currentModule;

        public MethodDefinitionGenerator(Model.Types.MethodDefinition methodDefinition, TypeReferenceGenerator typeReferenceGenerator, TypeDefinition containingType, ModuleDefinition module)
        {
            this.methodDefinition = methodDefinition;
            this.typeReferenceGenerator = typeReferenceGenerator;
            this.containingType = containingType;
            this.currentModule = module;

            Contract.Assert(typeReferenceGenerator != null);
            Contract.Assert(containingType != null);
            Contract.Assert(methodDefinition != null);
            Contract.Assert(currentModule != null);
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
                //if (localVariable.Type is Model.Types.PointerType)
                //    throw new NotImplementedException(); // we should specify if it is in/ref?

                var varDef = new VariableDefinition(typeReferenceGenerator.GenerateTypeReference(localVariable.Type));
                methodDef.Body.Variables.Add(varDef);
                variableDefinitions[localVariable] = varDef;
            }

            IDictionary<IVariable, ParameterDefinition> parameterDefinitions = new Dictionary<IVariable, ParameterDefinition>();
            foreach (IVariable localVariable in methodDefinition.Body.Parameters)
            {
                //if (localVariable.Type is Model.Types.PointerType)
                //    throw new NotImplementedException(); // we should specify if it is in/ref?

                if (localVariable.Name == "this")
                {
                    parameterDefinitions[localVariable] = methodDef.Body.ThisParameter;
                    continue;
                }

                var paramDef = new ParameterDefinition(typeReferenceGenerator.GenerateTypeReference(localVariable.Type));
                methodDef.Parameters.Add(paramDef);
                parameterDefinitions[localVariable] = paramDef;
            }

            ILProcessor ilProcessor = methodDef.Body.GetILProcessor();
            BytecodeTranslator translator = new BytecodeTranslator(methodDef, variableDefinitions, parameterDefinitions, typeReferenceGenerator, ilProcessor, currentModule);

            IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated 
                = new Dictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>>();

            // branch instruction targets are delayed until all mono cecil instruction objects are created 
            TranslateInstructions(translator, translated);
            TranslatePendingBranches(translated, methodDefinition.Body.Instructions.OfType<BranchInstruction>().ToList());

            foreach (Mono.Cecil.Cil.Instruction ins in translated.Values.SelectMany(l => l))
               ilProcessor.Append(ins);
            
            return methodDef;
        }
        
        private void TranslateInstructions(BytecodeTranslator translator, IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated)
        {
            foreach (Model.Bytecode.Instruction ins in methodDefinition.Body.Instructions)
            {
                ins.Accept(translator);
                Contract.Assert(translator.Result != null);
                translated[ins] = translator.Result;
            }
        }

        private void TranslatePendingBranches(IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated, IList<BranchInstruction> pending)
        {
            while (pending.Count > 0)
            {
                var br = pending.First();
                pending.Remove(br);

                TargetFinder targetFinder = new TargetFinder(translated);
                var result = translated[targetFinder.GetTarget(br.Target)];

                // this branch is pending on another pending branch
                if (result.Count > 0)
                {
                    translated[br].First().Operand = result.First();
                }
                else
                    pending.Add(br); // add as last element
            }
        }

        private class TargetFinder
        {
            public TargetFinder(IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated)
            {
                this.translated = translated;
            }

            public Model.Bytecode.Instruction GetTarget(string target)
            {
                var offset = ParseTarget(target);
                return translated.Where(kv => kv.Key.Offset == offset).Single().Key;
            }

            private uint ParseTarget(string target)
            {
                uint targetOffset;
                bool success = uint.TryParse(target.Replace("L_", ""), System.Globalization.NumberStyles.HexNumber, null, out targetOffset);
                if (!success)
                    throw new Exception();

                return targetOffset;
            }

            private IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated;
        }

        private class BytecodeTranslator : Model.Bytecode.Visitor.InstructionVisitor
        {
            private Mono.Cecil.MethodDefinition methodDefinition;
            private ILProcessor processor = null;
            private IDictionary<IVariable, VariableDefinition> variableDefinitions;
            private IDictionary<IVariable, ParameterDefinition> parameterDefinitions;
            private TypeReferenceGenerator typeReferenceGenerator;
            private ModuleDefinition currentModule;
            public IList<Mono.Cecil.Cil.Instruction> Result;

            public BytecodeTranslator(Mono.Cecil.MethodDefinition methodDefinition,
                IDictionary<IVariable, VariableDefinition> variableDefinitions,
                IDictionary<IVariable, ParameterDefinition> parameterDefinitions,
                TypeReferenceGenerator typeReferenceGenerator,
                ILProcessor processor,
                ModuleDefinition currentModule)
            {
                this.methodDefinition = methodDefinition;
                this.processor = processor;
                this.variableDefinitions = variableDefinitions;
                this.parameterDefinitions = parameterDefinitions;
                this.typeReferenceGenerator = typeReferenceGenerator;
                this.currentModule = currentModule;
            }

            private MethodReference GenerateMethodReference(Model.Types.IMethodReference method)
            {
                var methodReference = new MethodReference(method.Name,
                    typeReferenceGenerator.GenerateTypeReference(method.ReturnType),
                    typeReferenceGenerator.GenerateTypeReference(method.ContainingType));

                foreach (var param in method.Parameters)
                    methodReference.Parameters.Add(new ParameterDefinition(typeReferenceGenerator.GenerateTypeReference(param.Type)));

                if (!method.IsStatic)
                    methodReference.HasThis = true;

                return methodReference;
            }

            private FieldReference GenerateFieldReference(Model.Types.IFieldReference analysisNetFieldRef)
            {
                FieldReference fieldReference = new FieldReference(
                    analysisNetFieldRef.Name,
                    typeReferenceGenerator.GenerateTypeReference(analysisNetFieldRef.Type),
                    typeReferenceGenerator.GenerateTypeReference(analysisNetFieldRef.ContainingType)
                );

                return fieldReference;
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
                    case Model.Bytecode.BasicOperation.Div:
                        op = instruction.UnsignedOperands ? Mono.Cecil.Cil.OpCodes.Div_Un : Mono.Cecil.Cil.OpCodes.Div;
                        break;
                    case Model.Bytecode.BasicOperation.Rem:
                        op = instruction.UnsignedOperands ? Mono.Cecil.Cil.OpCodes.Rem_Un : Mono.Cecil.Cil.OpCodes.Rem;
                        break;
                    case Model.Bytecode.BasicOperation.Dup:
                        op = Mono.Cecil.Cil.OpCodes.Dup;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(op.Value) };
            }

            public override void Visit(Model.Bytecode.LoadIndirectInstruction instruction)
            {
                Mono.Cecil.Cil.OpCode opcode;
                if (instruction.Type == Model.Types.PlatformTypes.Float64)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_R8;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Float32)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_R4;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int64)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_I8;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int32)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_I4;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int16)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_I2;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int8)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_I1;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.IntPtr)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_I;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.UInt32)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_U4;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.UInt16)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_U2;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.UInt8)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_U1;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Object)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldind_Ref;
                }
                else if (instruction.Type.TypeKind == Model.Types.TypeKind.ValueType)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Ldobj;
                }
                else
                    throw new NotImplementedException();

                if (opcode != Mono.Cecil.Cil.OpCodes.Ldobj)
                    this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode) };
                else
                    this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode, typeReferenceGenerator.GenerateTypeReference(instruction.Type)) };
            }

            public override void Visit(Model.Bytecode.StoreIndirectInstruction instruction)
            {
                Mono.Cecil.Cil.OpCode opcode;

                if (instruction.Type == Model.Types.PlatformTypes.Float64)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_R8;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Float32)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_R4;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int64)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_I8;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int32)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_I4;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int16)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_I2;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Int8)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_I1;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.IntPtr)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_I;
                }
                else if (instruction.Type == Model.Types.PlatformTypes.Object)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stind_Ref;
                }
                else if (instruction.Type.TypeKind == Model.Types.TypeKind.ValueType)
                {
                    opcode = Mono.Cecil.Cil.OpCodes.Stobj;
                }
                else
                    throw new NotImplementedException();

                if (opcode != Mono.Cecil.Cil.OpCodes.Stobj)
                    this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode) };
                else
                    this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode, typeReferenceGenerator.GenerateTypeReference(instruction.Type)) };
            }

            public override void Visit(Model.Bytecode.LoadInstruction instruction)
            {
                Mono.Cecil.Cil.Instruction cilIns;
                if (instruction.Operation == Model.Bytecode.LoadOperation.Content)
                {
                    Contract.Assert(instruction.Operand is IVariable);
                    IVariable variable = instruction.Operand as IVariable;

                    if (variable.IsParameter)
                    {
                        if (variable.Name != "this")
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldarg, parameterDefinitions[variable]);
                        else
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldarg, 0);
                    }
                    else
                        cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldloc, variableDefinitions[variable]);

                }
                else if (instruction.Operation == Model.Bytecode.LoadOperation.Value)
                {
                    if (instruction.Operand is Constant constant)
                    {
                        if (constant.Value is sbyte asSbyte)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_S, asSbyte);
                        }
                        else if (constant.Value is byte asByte)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4, asByte);
                        }
                        else if (constant.Value is int asInt)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4, asInt);
                        }
                        else if (constant.Value is long asLong)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I8, asLong);
                        }
                        else if (constant.Value is float asFloat)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_R4, asFloat);
                        }
                        else if (constant.Value is double asDouble)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_R8, asDouble);
                        }
                        else if (constant.Value is string asString)
                        {
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4, asString);
                        }
                        else if (constant.Value == null){
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldnull);
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

                } else if (instruction.Operation == Model.Bytecode.LoadOperation.Address)
                {
                    if (instruction.Operand is IVariable variable)
                    {
                        if (variable.IsParameter)
                        {
                            if (variable.Name != "this")
                                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldloca, parameterDefinitions[variable]);
                            else
                                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldloca, 0);
                        }
                        else
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldloca, variableDefinitions[variable]);
                    }
                    else
                        throw new NotImplementedException();
                }
                else
                    throw new NotImplementedException();

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.LoadFieldInstruction instruction)
            {
                FieldReference fieldReference = GenerateFieldReference(instruction.Field);
                Mono.Cecil.Cil.Instruction cilIns;
                if (instruction.Operation == Model.Bytecode.LoadFieldOperation.Content)
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldfld, fieldReference);
                else
                    throw new NotImplementedException();

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.LoadMethodAddressInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.StoreInstruction instruction)
            {
                Mono.Cecil.Cil.Instruction cilIns;
                if (instruction.Target.IsParameter)
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Starg, parameterDefinitions[instruction.Target]);
                else
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Stloc, variableDefinitions[instruction.Target]);

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.StoreFieldInstruction instruction)
            {
                Mono.Cecil.Cil.Instruction cilIns;
                FieldReference fieldReference = GenerateFieldReference(instruction.Field);

                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Stfld, fieldReference);
                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.ConvertInstruction instruction) { throw new NotImplementedException(); }

            private Mono.Cecil.Cil.OpCode GetCilBranchOpcode(Model.Bytecode.BranchInstruction instruction)
            {
                switch (instruction.Operation)
                {
                    case BranchOperation.Branch:
                        return Mono.Cecil.Cil.OpCodes.Br;
                    case BranchOperation.Eq:
                        return Mono.Cecil.Cil.OpCodes.Beq;
                    case BranchOperation.False:
                        return Mono.Cecil.Cil.OpCodes.Brfalse;
                    case BranchOperation.Ge:
                        if (instruction.UnsignedOperands)
                            return Mono.Cecil.Cil.OpCodes.Bge_Un;
                        else
                            return Mono.Cecil.Cil.OpCodes.Bge;
                    case BranchOperation.Gt:
                        if (instruction.UnsignedOperands)
                            return Mono.Cecil.Cil.OpCodes.Bgt_Un;
                        else
                            return Mono.Cecil.Cil.OpCodes.Bgt;
                    case BranchOperation.Le:
                        if (instruction.UnsignedOperands)
                            return Mono.Cecil.Cil.OpCodes.Ble_Un;
                        else
                            return Mono.Cecil.Cil.OpCodes.Ble;
                    case BranchOperation.Leave:
                        throw new NotImplementedException();
                    case BranchOperation.Lt:
                        if (instruction.UnsignedOperands)
                            return Mono.Cecil.Cil.OpCodes.Blt_Un;
                        else
                            return Mono.Cecil.Cil.OpCodes.Blt;
                    case BranchOperation.Neq:
                        return Mono.Cecil.Cil.OpCodes.Bne_Un;
                    case BranchOperation.True:
                        return Mono.Cecil.Cil.OpCodes.Brtrue;
                    default:
                        throw new NotImplementedException();
                }
            }

            public override void Visit(Model.Bytecode.BranchInstruction instruction)
            {
                // placeholder for target as a nop
                var cilIns = processor.Create(GetCilBranchOpcode(instruction), processor.Create(OpCodes.Nop));
                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.SwitchInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.SizeofInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.LoadTokenInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.MethodCallInstruction instruction)
            {
                Mono.Cecil.Cil.Instruction cilIns;
                var methodReference = GenerateMethodReference(instruction.Method);

                if (instruction.Operation == Model.Bytecode.MethodCallOperation.Static)
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Call, methodReference);
                else if (instruction.Operation == Model.Bytecode.MethodCallOperation.Virtual)
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Callvirt, methodReference);
                else
                    throw new NotImplementedException();

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }
            public override void Visit(Model.Bytecode.IndirectMethodCallInstruction instruction) { throw new NotImplementedException(); }
            public override void Visit(Model.Bytecode.CreateObjectInstruction instruction)
            {
                var methodReference = GenerateMethodReference(instruction.Constructor);
                var cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Newobj, methodReference);
                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.CreateArrayInstruction instruction) {

                if (instruction.WithLowerBound)
                    throw new NotImplementedException();

                var cilArrayType = typeReferenceGenerator.GenerateTypeReference(instruction.Type);

                Mono.Cecil.Cil.Instruction cilIns = null;
                if (!instruction.Type.IsVector)
                {
                    var arrayCtor = ArrayHelper.ArrayCtor(cilArrayType as ArrayType);
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Newobj, arrayCtor);
                } else
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Newarr, cilArrayType);

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
            }

            public override void Visit(Model.Bytecode.LoadArrayElementInstruction instruction)
            {
                if (instruction.WithLowerBound)
                    throw new NotImplementedException();

                Mono.Cecil.Cil.Instruction res = null;

                if (instruction.Operation == LoadArrayElementOperation.Address)
                {
                    if (!instruction.Array.IsVector)
                    {
                        var arrayAddress = ArrayHelper.ArrayAddress(typeReferenceGenerator.GenerateTypeReference(instruction.Array) as ArrayType);
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Call, arrayAddress);
                    } else
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelema, typeReferenceGenerator.GenerateTypeReference(instruction.Array.ElementsType));
                }
                else if (instruction.Operation == LoadArrayElementOperation.Content)
                {
                    if (!instruction.Array.IsVector)
                    {
                        var arrayGet = ArrayHelper.ArrayGet(typeReferenceGenerator.GenerateTypeReference(instruction.Array) as ArrayType);

                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Call, arrayGet);

                    } else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.IntPtr)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_I);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int8)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_I1);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int16)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_I2);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int32)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_I4);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int64)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_I8);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float32)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_R4);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float64)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_R8);
                    }
                    else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float64)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_R8);
                    }
                    else if (instruction.Array.ElementsType.TypeKind == Model.Types.TypeKind.ReferenceType)
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_Ref);
                    }
                    else
                    {
                        res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_Any, typeReferenceGenerator.GenerateTypeReference(instruction.Array.ElementsType));
                    }
                }
                else
                    throw new NotImplementedException();

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { res };
            }

            public override void Visit(Model.Bytecode.StoreArrayElementInstruction instruction)
            {
                if (instruction.WithLowerBound)
                    throw new NotImplementedException();

                Mono.Cecil.Cil.Instruction res = null;

                if (!instruction.Array.IsVector)
                {
                    var arrayGet = ArrayHelper.ArraySet(typeReferenceGenerator.GenerateTypeReference(instruction.Array) as ArrayType);
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Call, arrayGet);
                } else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.IntPtr)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I);
                } else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int8)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I1);
                } else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int16)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I2);
                }
                else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int32)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I4);
                } else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int64)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I8);
                }
                else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float32)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_R4);
                } else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float64)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_R8);
                }
                else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float64)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_R8);
                } else if (instruction.Array.ElementsType.TypeKind == Model.Types.TypeKind.ReferenceType)
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_Ref);
                } else
                {
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_Any, typeReferenceGenerator.GenerateTypeReference(instruction.Array.ElementsType));
                }

                this.Result = new List<Mono.Cecil.Cil.Instruction>() { res };
            }

        }
    }
}
