using System;
using System.Collections.Generic;
using System.Text;
using Cecil = Mono.Cecil;
using AnalysisNet = Model;
using AnalysisNetTac = Model.ThreeAddressCode;

using System.Diagnostics.Contracts;
using System.Linq;

namespace CodeGenerator.CecilCodeGenerator
{
    internal class TargetFinder
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

    internal class BytecodeTranslator : Model.Bytecode.Visitor.InstructionVisitor
    {
        private AnalysisNet.Types.MethodDefinition methodDefinition;
        private Cecil.Cil.ILProcessor processor = null;
        private IDictionary<AnalysisNetTac.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions;
        private IDictionary<AnalysisNetTac.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions;
        private ReferenceGenerator referenceGenerator;
        public IList<Cecil.Cil.Instruction> Result;

        public BytecodeTranslator(AnalysisNet.Types.MethodDefinition methodDefinition,
            IDictionary<AnalysisNetTac.Values.IVariable, Cecil.Cil.VariableDefinition> variableDefinitions,
            IDictionary<AnalysisNetTac.Values.IVariable, Cecil.ParameterDefinition> parameterDefinitions,
            ReferenceGenerator referenceGenerator,
            Cecil.Cil.ILProcessor processor)
        {
            this.methodDefinition = methodDefinition;
            this.processor = processor;
            this.variableDefinitions = variableDefinitions;
            this.parameterDefinitions = parameterDefinitions;
            this.referenceGenerator = referenceGenerator;
        }

        public IEnumerable<Cecil.Cil.Instruction> Translate()
        {
            IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated
                = new Dictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>>();

            // branch instruction targets are delayed until all mono cecil instruction objects are created 
            foreach (Model.Bytecode.Instruction ins in methodDefinition.Body.Instructions)
            {
                ins.Accept(this);
                Contract.Assert(this.Result != null);
                translated[ins] = this.Result;
            }

            IEnumerable<AnalysisNet.Bytecode.Instruction> branches = methodDefinition.Body.Instructions.OfType<AnalysisNet.Bytecode.BranchInstruction>();
            IEnumerable<AnalysisNet.Bytecode.Instruction> switches = methodDefinition.Body.Instructions.OfType<AnalysisNet.Bytecode.SwitchInstruction>();
            var union = branches.Union(switches);
            TranslatePendingBranches(translated, union.ToList());

            return translated.Values.SelectMany(l => l);
        }
        private void TranslatePendingBranches(IDictionary<Model.Bytecode.Instruction, IList<Mono.Cecil.Cil.Instruction>> translated, IList<AnalysisNet.Bytecode.Instruction> pending)
        {
            TargetFinder targetFinder = new TargetFinder(translated);

            while (pending.Count > 0)
            {
                var ins = pending.First();
                pending.Remove(ins);

                if (ins is AnalysisNet.Bytecode.BranchInstruction br)
                {
                    var result = translated[targetFinder.GetTarget(br.Target)].First();
                    translated[br].First().Operand = result;
                }
                else if (ins is AnalysisNet.Bytecode.SwitchInstruction switchIns)
                {
                    for (int idx = 0; idx < switchIns.Targets.Count; idx++)
                    {
                        var target = switchIns.Targets[idx];
                        var result = translated[targetFinder.GetTarget(target)].First();
                        var cecilTargets = translated[switchIns].First().Operand as Cecil.Cil.Instruction[];
                        cecilTargets[idx] = result;
                    }
                }
                else
                    throw new NotImplementedException();
            }
        }

        public override void Visit(Model.Bytecode.ConstrainedInstruction instruction)
        {
            Mono.Cecil.Cil.Instruction ins = processor.Create(Mono.Cecil.Cil.OpCodes.Constrained, referenceGenerator.TypeReference(instruction.ThisType));
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { ins };
        }
        public override void Visit(Model.Bytecode.InitObjInstruction instruction)
        {
            var op = Cecil.Cil.OpCodes.Initobj;
            var type = referenceGenerator.TypeReference(instruction.Type);
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(op, type) };
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
                case Model.Bytecode.BasicOperation.Sub:
                    if (instruction.OverflowCheck && instruction.UnsignedOperands)
                    {
                        op = Mono.Cecil.Cil.OpCodes.Sub_Ovf_Un;
                    }
                    else if (instruction.OverflowCheck && !instruction.UnsignedOperands)
                    {
                        op = Mono.Cecil.Cil.OpCodes.Sub_Ovf;
                    }
                    else if (!instruction.OverflowCheck && !instruction.UnsignedOperands)
                    {
                        op = Mono.Cecil.Cil.OpCodes.Sub;
                    }
                    else
                        throw new NotImplementedException();
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
                case Model.Bytecode.BasicOperation.Throw:
                    op = Mono.Cecil.Cil.OpCodes.Throw;
                    break;
                case Model.Bytecode.BasicOperation.EndFinally:
                    // we don't support exceptions yet
                    op = Mono.Cecil.Cil.OpCodes.Nop;
                    break;
                case Model.Bytecode.BasicOperation.Shl:
                    op = Mono.Cecil.Cil.OpCodes.Shl;
                    break;
                case Model.Bytecode.BasicOperation.Shr:
                    op = instruction.UnsignedOperands ? Mono.Cecil.Cil.OpCodes.Shr_Un : Mono.Cecil.Cil.OpCodes.Shr;
                    break;
                case Model.Bytecode.BasicOperation.Mul:
                    if (instruction.OverflowCheck && instruction.UnsignedOperands)
                        op = Mono.Cecil.Cil.OpCodes.Mul_Ovf_Un;
                    else if (instruction.OverflowCheck)
                        op = Mono.Cecil.Cil.OpCodes.Mul_Ovf;
                    else
                        op = Mono.Cecil.Cil.OpCodes.Mul;
                    break;
                case Model.Bytecode.BasicOperation.Eq:
                    op = Mono.Cecil.Cil.OpCodes.Ceq;
                    break;
                case Model.Bytecode.BasicOperation.Gt:
                    if (!instruction.UnsignedOperands)
                        op = Mono.Cecil.Cil.OpCodes.Cgt;
                    else
                        op = Mono.Cecil.Cil.OpCodes.Cgt_Un;
                    break;
                case Model.Bytecode.BasicOperation.Lt:
                    if (!instruction.UnsignedOperands)
                        op = Mono.Cecil.Cil.OpCodes.Clt;
                    else
                        op = Mono.Cecil.Cil.OpCodes.Clt_Un;
                    break;
                case Model.Bytecode.BasicOperation.Or:
                    op = Mono.Cecil.Cil.OpCodes.Or;
                    break;
                case Model.Bytecode.BasicOperation.LoadArrayLength:
                    op = Mono.Cecil.Cil.OpCodes.Ldlen;
                    break;
                case Model.Bytecode.BasicOperation.And:
                    op = Mono.Cecil.Cil.OpCodes.And;
                    break;
                case Model.Bytecode.BasicOperation.Xor:
                    op = Mono.Cecil.Cil.OpCodes.Xor;
                    break;
                case Model.Bytecode.BasicOperation.Not:
                    op = Mono.Cecil.Cil.OpCodes.Not;
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
                this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode, referenceGenerator.TypeReference(instruction.Type)) };
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
            else if (instruction.Type.TypeKind == Model.Types.TypeKind.Unknown && instruction.Type is AnalysisNet.Types.IGenericParameterReference)
            {
                // the idea is to contemplate cases where the type is a generic type
                // however, i think we should have to check the type constraints.
                opcode = Mono.Cecil.Cil.OpCodes.Stobj;
            }
            else
                throw new NotImplementedException();

            if (opcode != Mono.Cecil.Cil.OpCodes.Stobj)
                this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode) };
            else
                this.Result = new List<Mono.Cecil.Cil.Instruction>() { processor.Create(opcode, referenceGenerator.TypeReference(instruction.Type)) };
        }

        public override void Visit(Model.Bytecode.LoadInstruction instruction)
        {
            Mono.Cecil.Cil.Instruction cilIns;
            if (instruction.Operation == Model.Bytecode.LoadOperation.Content)
            {
                Contract.Assert(instruction.Operand is AnalysisNetTac.Values.IVariable);
                AnalysisNetTac.Values.IVariable variable = instruction.Operand as AnalysisNetTac.Values.IVariable;

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
                if (instruction.Operand is AnalysisNetTac.Values.Constant constant)
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
                        cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldstr, asString);
                    }
                    else if (constant.Value == null)
                    {
                        cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldnull);
                    }
                    else
                        throw new NotImplementedException();
                }
                else if (instruction.Operand is AnalysisNetTac.Values.IVariable variable)
                {
                    throw new NotImplementedException();
                }
                else if (instruction.Operand is AnalysisNetTac.Values.UnknownValue unk)
                {
                    throw new NotImplementedException();
                }
                else
                    throw new NotImplementedException();

            }
            else if (instruction.Operation == Model.Bytecode.LoadOperation.Address)
            {
                if (instruction.Operand is AnalysisNetTac.Values.IVariable variable)
                {
                    if (variable.IsParameter)
                    {
                        if (variable.Name != "this")
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldarga, parameterDefinitions[variable]);
                        else
                            cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldarga, 0);
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
            Cecil.FieldReference fieldReference = referenceGenerator.FieldReference(instruction.Field);
            Mono.Cecil.Cil.Instruction cilIns;
            if (instruction.Operation == Model.Bytecode.LoadFieldOperation.Content)
            {
                if (!instruction.Field.IsStatic)
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldfld, fieldReference);
                else
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldsfld, fieldReference);
            }
            else
            {
                if (!instruction.Field.IsStatic)
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldflda, fieldReference);
                else
                    cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Ldsflda, fieldReference);
            }

            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }

        public override void Visit(Model.Bytecode.LoadMethodAddressInstruction instruction) {

            var op = Cecil.Cil.OpCodes.Ldftn;

            if (instruction.Operation == AnalysisNet.Bytecode.LoadMethodAddressOperation.Virtual)
                op = Cecil.Cil.OpCodes.Ldvirtftn;

            var cilIns = processor.Create(op, referenceGenerator.MethodReference(instruction.Method));
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }
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
            Cecil.FieldReference fieldReference = referenceGenerator.FieldReference(instruction.Field);

            if (!instruction.Field.IsStatic)
                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Stfld, fieldReference);
            else
                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Stsfld, fieldReference);

            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }
        
        private Cecil.Cil.OpCode GetConvOpcode(Model.Bytecode.ConvertInstruction instruction)
        {
            if (instruction.OverflowCheck)
            {
                if (instruction.UnsignedOperands)
                {
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int8))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I1_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int16))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I2_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int32))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I4_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int64))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I8_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.IntPtr))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt8))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U1_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt16))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U2_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt32))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U4_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt64))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U8_Un;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UIntPtr))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U_Un;
                }
                else
                {
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int8))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I1;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int16))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I2;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int32))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I4;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int64))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I8;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.IntPtr))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_I;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt8))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U1;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt16))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U2;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt32))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U4;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt64))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U8;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UIntPtr))
                        return Mono.Cecil.Cil.OpCodes.Conv_Ovf_U;
                }

            }
            else
            {
                if (instruction.UnsignedOperands &&
                    instruction.ConversionType.Equals(Model.Types.PlatformTypes.Float32))
                    return Mono.Cecil.Cil.OpCodes.Conv_R_Un;

                if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Float32))
                    return Mono.Cecil.Cil.OpCodes.Conv_R4;
                if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Float64))
                    return Mono.Cecil.Cil.OpCodes.Conv_R8;

                if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int8))
                        return Mono.Cecil.Cil.OpCodes.Conv_I1;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int16))
                        return Mono.Cecil.Cil.OpCodes.Conv_I2;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int32))
                        return Mono.Cecil.Cil.OpCodes.Conv_I4;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.Int64))
                        return Mono.Cecil.Cil.OpCodes.Conv_I8;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.IntPtr))
                        return Mono.Cecil.Cil.OpCodes.Conv_I;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt8))
                        return Mono.Cecil.Cil.OpCodes.Conv_U1;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt16))
                        return Mono.Cecil.Cil.OpCodes.Conv_U2;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt32))
                        return Mono.Cecil.Cil.OpCodes.Conv_U4;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UInt64))
                        return Mono.Cecil.Cil.OpCodes.Conv_U8;
                    if (instruction.ConversionType.Equals(Model.Types.PlatformTypes.UIntPtr))
                        return Mono.Cecil.Cil.OpCodes.Conv_U;
            }

            throw new NotImplementedException();
        }
        public override void Visit(Model.Bytecode.ConvertInstruction instruction)
        {
            Mono.Cecil.Cil.Instruction cilIns;

            if (instruction.Operation == AnalysisNet.Bytecode.ConvertOperation.Box)
            {
                cilIns = processor.Create(Cecil.Cil.OpCodes.Box, referenceGenerator.TypeReference(instruction.ConversionType));
            } else if (instruction.Operation == AnalysisNet.Bytecode.ConvertOperation.Conv)
            {
                cilIns = processor.Create(GetConvOpcode(instruction));
            } else if (instruction.Operation == AnalysisNet.Bytecode.ConvertOperation.Cast)
            {
                cilIns = processor.Create(Cecil.Cil.OpCodes.Castclass, referenceGenerator.TypeReference(instruction.ConversionType));
            }
            else if (instruction.Operation == AnalysisNet.Bytecode.ConvertOperation.Unbox)
            {
                throw new NotImplementedException();
            } else if (instruction.Operation == AnalysisNet.Bytecode.ConvertOperation.UnboxPtr)
            {
                throw new NotImplementedException();
            } else
                throw new NotImplementedException();

            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }

        private Mono.Cecil.Cil.OpCode GetCilBranchOpcode(Model.Bytecode.BranchInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case AnalysisNet.Bytecode.BranchOperation.Branch:
                    return Mono.Cecil.Cil.OpCodes.Br;
                case AnalysisNet.Bytecode.BranchOperation.Eq:
                    return Mono.Cecil.Cil.OpCodes.Beq;
                case AnalysisNet.Bytecode.BranchOperation.False:
                    return Mono.Cecil.Cil.OpCodes.Brfalse;
                case AnalysisNet.Bytecode.BranchOperation.Ge:
                    if (instruction.UnsignedOperands)
                        return Mono.Cecil.Cil.OpCodes.Bge_Un;
                    else
                        return Mono.Cecil.Cil.OpCodes.Bge;
                case AnalysisNet.Bytecode.BranchOperation.Gt:
                    if (instruction.UnsignedOperands)
                        return Mono.Cecil.Cil.OpCodes.Bgt_Un;
                    else
                        return Mono.Cecil.Cil.OpCodes.Bgt;
                case AnalysisNet.Bytecode.BranchOperation.Le:
                    if (instruction.UnsignedOperands)
                        return Mono.Cecil.Cil.OpCodes.Ble_Un;
                    else
                        return Mono.Cecil.Cil.OpCodes.Ble;
                case AnalysisNet.Bytecode.BranchOperation.Leave:
                    return Mono.Cecil.Cil.OpCodes.Leave;
                case AnalysisNet.Bytecode.BranchOperation.Lt:
                    if (instruction.UnsignedOperands)
                        return Mono.Cecil.Cil.OpCodes.Blt_Un;
                    else
                        return Mono.Cecil.Cil.OpCodes.Blt;
                case AnalysisNet.Bytecode.BranchOperation.Neq:
                    return Mono.Cecil.Cil.OpCodes.Bne_Un;
                case AnalysisNet.Bytecode.BranchOperation.True:
                    return Mono.Cecil.Cil.OpCodes.Brtrue;
                default:
                    throw new NotImplementedException();
            }
        }

        public override void Visit(Model.Bytecode.BranchInstruction instruction)
        {
            // placeholder for target as a nop
            var cilIns = processor.Create(GetCilBranchOpcode(instruction), processor.Create(Cecil.Cil.OpCodes.Nop));
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }

        public override void Visit(Model.Bytecode.SwitchInstruction instruction)
        {
            var targets = instruction.Targets.Select(t => processor.Create(Cecil.Cil.OpCodes.Nop));
            var cilIns = processor.Create(Cecil.Cil.OpCodes.Switch, targets.ToArray());
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }
        public override void Visit(Model.Bytecode.SizeofInstruction instruction) { throw new NotImplementedException(); }
        public override void Visit(Model.Bytecode.LoadTokenInstruction instruction) 
        {
            Cecil.Cil.Instruction cilIns;
            if (instruction.Token is AnalysisNet.Types.IType type)
            {
                var token = referenceGenerator.TypeReference(type);
                cilIns = processor.Create(Cecil.Cil.OpCodes.Ldtoken, token);
            }
            else if (instruction.Token is AnalysisNet.Types.IFieldReference field)
            {
                var token = referenceGenerator.FieldReference(field);
                cilIns = processor.Create(Cecil.Cil.OpCodes.Ldtoken, token);
            }
            else
                throw new NotImplementedException();
            
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }
        public override void Visit(Model.Bytecode.MethodCallInstruction instruction)
        {
            Mono.Cecil.Cil.Instruction cilIns;
            var methodReference = referenceGenerator.MethodReference(instruction.Method);

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
            var methodReference = referenceGenerator.MethodReference(instruction.Constructor);
            var cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Newobj, methodReference);
            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }

        public override void Visit(Model.Bytecode.CreateArrayInstruction instruction)
        {
            var cilArrayType = referenceGenerator.TypeReference(instruction.Type) as Cecil.ArrayType;

            Mono.Cecil.Cil.Instruction cilIns = null;
            if (!instruction.Type.IsVector)
            {
                var arrayCtor = ArrayHelper.ArrayCtor(cilArrayType as Cecil.ArrayType);
                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Newobj, arrayCtor);
            }
            else
                cilIns = processor.Create(Mono.Cecil.Cil.OpCodes.Newarr, cilArrayType.ElementType);

            this.Result = new List<Mono.Cecil.Cil.Instruction>() { cilIns };
        }

        public override void Visit(Model.Bytecode.LoadArrayElementInstruction instruction)
        {
            Mono.Cecil.Cil.Instruction res = null;

            if (instruction.Operation == AnalysisNet.Bytecode.LoadArrayElementOperation.Address)
            {
                if (!instruction.Array.IsVector)
                {
                    var arrayAddress = ArrayHelper.ArrayAddress(referenceGenerator.TypeReference(instruction.Array) as Cecil.ArrayType);
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Call, arrayAddress);
                }
                else
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelema, referenceGenerator.TypeReference(instruction.Array.ElementsType));
            }
            else if (instruction.Operation == AnalysisNet.Bytecode.LoadArrayElementOperation.Content)
            {
                if (!instruction.Array.IsVector)
                {
                    var arrayGet = ArrayHelper.ArrayGet(referenceGenerator.TypeReference(instruction.Array) as Cecil.ArrayType);

                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Call, arrayGet);

                }
                else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.IntPtr)
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
                    res = processor.Create(Mono.Cecil.Cil.OpCodes.Ldelem_Any, referenceGenerator.TypeReference(instruction.Array.ElementsType));
                }
            }
            else
                throw new NotImplementedException();

            this.Result = new List<Mono.Cecil.Cil.Instruction>() { res };
        }

        public override void Visit(Model.Bytecode.StoreArrayElementInstruction instruction)
        {
            Mono.Cecil.Cil.Instruction res = null;

            if (!instruction.Array.IsVector)
            {
                var arraySet = ArrayHelper.ArraySet(referenceGenerator.TypeReference(instruction.Array) as Cecil.ArrayType);
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Call, arraySet);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.IntPtr)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int8)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I1);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int16)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I2);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int32)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I4);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Int64)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_I8);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float32)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_R4);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float64)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_R8);
            }
            else if (instruction.Array.ElementsType == Model.Types.PlatformTypes.Float64)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_R8);
            }
            else if (instruction.Array.ElementsType.TypeKind == Model.Types.TypeKind.ReferenceType)
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_Ref);
            }
            else
            {
                res = processor.Create(Mono.Cecil.Cil.OpCodes.Stelem_Any, referenceGenerator.TypeReference(instruction.Array.ElementsType));
            }

            this.Result = new List<Mono.Cecil.Cil.Instruction>() { res };
        }

    }
}
