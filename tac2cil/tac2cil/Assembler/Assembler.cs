using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using Model.ThreeAddressCode.Values;
using Bytecode = Model.Bytecode;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System;
using Model;

namespace tac2cil.Assembler
{
    public class Assembler
    {
        private MethodBody _body;

        public MethodBody Execute()
        {
            MethodBody body = new MethodBody(MethodBodyKind.Bytecode);

            body.Parameters.AddRange(_body.Parameters);
            body.LocalVariables.UnionWith(_body.LocalVariables);
            body.ExceptionInformation.AddRange(_body.ExceptionInformation);
            body.MaxStack = _body.MaxStack;
            
            InstructionConverter instructionConverter = new InstructionConverter();
            foreach (Instruction tac in _body.Instructions)
                tac.Accept(instructionConverter);

            body.Instructions.AddRange(instructionConverter.Result);

            return body;
        }

        public Assembler(MethodBody b)
        {
            Contract.Assert(b.Kind == MethodBodyKind.ThreeAddressCode);
            _body = b;
        }

        private class InstructionConverter : InstructionVisitor
        {
            public IList<Bytecode.Instruction> Result { get; private set; }
                = new List<Bytecode.Instruction>();

            private Bytecode.Instruction LoadOperand(IVariable variable, string label = null)
            {
                var l = new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Content, variable);
                if (label != null)
                    l.Label = label;
                return l;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                Result.Add(LoadOperand(instruction.LeftOperand));
                Result.Add(LoadOperand(instruction.RightOperand));
                Bytecode.BasicInstruction basic = new Bytecode.BasicInstruction(instruction.Offset, instruction.Operation.ToBasicOperation())
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                Result.Add(basic);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(LoadInstruction instruction)
            {
                if (instruction.Operand is Constant constant)
                {
                    Bytecode.LoadInstruction loadInstruction = new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Value, constant);
                    loadInstruction.Label = instruction.Label;
                    Result.Add(loadInstruction);
                    return;
                }

                throw new NotImplementedException();
            }

            public override void Visit(StoreInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(NopInstruction instruction)
            {
                Result.Add(new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop));
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(TryInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(FaultInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(FinallyInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(FilterInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(CatchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(ConvertInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(ReturnInstruction instruction)
            {
                Result.Add(new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Return));
            }

            public override void Visit(ThrowInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(BranchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(ExceptionalBranchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(SwitchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(SizeofInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new NotImplementedException();
            }
        }
    }
}
