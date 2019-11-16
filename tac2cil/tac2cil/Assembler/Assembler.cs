using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using Model.ThreeAddressCode.Values;
using Bytecode = Model.Bytecode;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System;
using Model;
using Backend.Analyses;
using Backend.Model;
using System.Linq;

namespace tac2cil.Assembler
{
    public class Assembler
    {
        #region class OperandStack
        private class OperandStack
        {
            private readonly Stack<bool> _s;

            public OperandStack()
            {
                MaxCapacity = 0;
                _s = new Stack<bool>();
            }

            public IEnumerable<bool> Variables
            {
                get { return _s; }
            }

            public ushort MaxCapacity { get; private set; }

            public ushort Size
            {
                get { return (ushort)_s.Count; }
                set
                {
                    if (value < 0) throw new InvalidOperationException();

                    if (value >= _s.Count)
                    {
                        for (int i = 0; i > (value - _s.Count); i++)
                            _s.Push(true);
                    }
                    else
                    {
                        for (int i = 0; i > (_s.Count - value); i++)
                            _s.Pop();
                    }
                }
            }

            //public void Clear()
            //{
            //    top = 0;
            //}

            //public void IncrementCapacity()
            //{
            //    if (capacity >= stack.Length) throw new InvalidOperationException();
            //    capacity++;
            //}

            //public void DecrementCapacity()
            //{
            //    if (capacity <= stack.Length - 1) throw new InvalidOperationException();
            //    capacity--;
            //}

            public void Push()
            {
                if (_s.Count >= MaxCapacity)
                    MaxCapacity++;

                _s.Push(true);
            }

            public void Pop()
            {
                _s.Pop();
            }
        }

        #endregion

        private MethodBody _tacBody;
        private OperandStack _stack;

        public Assembler(MethodBody b)
        {
            Contract.Assert(b.Kind == MethodBodyKind.ThreeAddressCode);
            _tacBody = b;
            _stack = new OperandStack();
        }

        public MethodBody Execute()
        {
            MethodBody bytecodeBody = new MethodBody(MethodBodyKind.Bytecode);

            bytecodeBody.Parameters.AddRange(_tacBody.Parameters);
            bytecodeBody.LocalVariables.AddRange(_tacBody.LocalVariables);
            //bytecodeBody.ExceptionInformation.AddRange(_tacBody.ExceptionInformation);
            bytecodeBody.MaxStack = 0;

            if (_tacBody.Instructions.Count > 0)
            {
                InstructionConverter instructionConverter = new InstructionConverter(_stack);
                var cfanalysis = new ControlFlowAnalysis(_tacBody);
                // exceptions disabled for now
                var cfg = cfanalysis.GenerateNormalControlFlow();
                var stackSizeAtEntry = new ushort?[cfg.Nodes.Count];
                var sorted_nodes = cfg.ForwardOrder;

                //FillExceptionHandlersStart();
                foreach (var node in sorted_nodes)
                {
                    var stackSize = stackSizeAtEntry[node.Id];

                    if (!stackSize.HasValue)
                        stackSizeAtEntry[node.Id] = 0;

                    _stack.Size = stackSizeAtEntry[node.Id].Value;
                    this.ProcessBasicBlock(bytecodeBody, node, instructionConverter);

                    foreach (var successor in node.Successors)
                    {
                        // exceptions disabled for now
                        //var successorIsHandlerHeader = false;
                        //if (successor.Instructions.Count > 0)
                        //{
                        //var firstInstruction = successor.Instructions.First();
                        //successorIsHandlerHeader = exceptionHandlersStart.ContainsKey(firstInstruction.Label);
                        //}

                        stackSize = stackSizeAtEntry[successor.Id];

                        if (!stackSize.HasValue)
                            stackSizeAtEntry[successor.Id] = _stack.Size;

                        else if (stackSize.Value != _stack.Size /*&& !successorIsHandlerHeader*/)
                        {
                            // Check that the already saved stack size is the same as the current stack size
                            throw new Exception("Basic block with different stack size at entry!");
                        }
                    }
                }

                bytecodeBody.MaxStack = (ushort)_stack.MaxCapacity;
                bytecodeBody.Instructions.AddRange(instructionConverter.Result);
                //EmptyStackBeforeExit(bytecodeBody, (int)stackSizeAtEntry[cfg.Exit.Id]);
            }

            return bytecodeBody;
        }

        private void EmptyStackBeforeExit(MethodBody methodBody, int sizeAtExit)
        {
            Contract.Assert(methodBody.Kind.Equals(MethodBodyKind.Bytecode));
            Contract.Assert(methodBody.Instructions.All(i => i.Offset == 0));

            // all return instructions have the same label and offset, therefore they are all equal between them
            // we look for the indexes of their appearences
            var returnIndexes = methodBody.Instructions.Select((ins,indx) => ins is Bytecode.BasicInstruction basic &&
                                                                                    basic.Operation.Equals(Bytecode.BasicOperation.Return) 
                                                                                    ? indx : -1)
                                                                                    .Where(idx => idx >= 0);

            // number of pops that we must execute before returning
            var pops = Enumerable.Repeat(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Pop), sizeAtExit);

            for (int i = 0; i < returnIndexes.Count(); i++)
            {
                foreach (var pop in pops)
                {
                    // ElementAt is updated by the changes in the list!
                    // after an insertion we get the updated index of the next return instruction
                    int retIdx = returnIndexes.ElementAt(i);
                    methodBody.Instructions.Insert(retIdx, pop);
                }
            }
        } 

        private void ProcessBasicBlock(MethodBody body, CFGNode node, InstructionConverter translator)
        {
            if (node.Instructions.Count == 0) return;

            var firstInstruction = node.Instructions.First();
            //ProcessExceptionHandling(body, firstInstruction);

            translator.Visit(node);
        }

        private void ProcessExceptionHandling(MethodBody body, IInstruction operation)
        {
            // todo: check the disassembler code
            throw new NotImplementedException();
        }

        private class InstructionConverter : InstructionVisitor
        {
            public IList<Bytecode.Instruction> Result { get; private set; }
                = new List<Bytecode.Instruction>();

            private readonly OperandStack _stack;


            private void AddWithLabel(IEnumerable<Bytecode.Instruction> instructions, string label)
            {
                instructions.First().Label = label;
                Result.AddRange(instructions);
            }

            public InstructionConverter(OperandStack stack)
            {
                _stack = stack;
            }

            private Bytecode.Instruction LoadOperand(IVariable variable)
            {
                var l = new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Content, variable);
                _stack.Push();
                return l;
            }
            private Bytecode.Instruction StoreOperand(IVariable Result)
            {
                throw new NotImplementedException();
                //var l = new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Content, variable);
                //_stack.Push();
                //return l;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>();

                instructions.Add(LoadOperand(instruction.LeftOperand));
                instructions.Add(LoadOperand(instruction.RightOperand));
                Bytecode.BasicInstruction basic = new Bytecode.BasicInstruction(0, instruction.Operation.ToBasicOperation())
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands,
                };
                instructions.Add(basic);
                instructions.Add(StoreOperand(instruction.Result));
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(LoadOperand(instruction.Operand));
                Bytecode.BasicInstruction basic = new Bytecode.BasicInstruction(0, instruction.Operation.ToUnaryOperation());
                instructions.Add(basic);
                instructions.Add(StoreOperand(instruction.Result));
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(LoadInstruction instruction)
            {
                if (instruction.Operand is Constant constant)
                {
                    var instructions = new List<Bytecode.Instruction>();

                    Bytecode.LoadInstruction loadInstruction = new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Value, constant);
                    instructions.Add(loadInstruction);

                    Bytecode.StoreInstruction storeInstruction = new Bytecode.StoreInstruction(0, instruction.Result);
                    instructions.Add(storeInstruction);

                    AddWithLabel(instructions, instruction.Label);
                }

                throw new NotImplementedException();
            }

            public override void Visit(StoreInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(NopInstruction instruction)
            {
                Result.Add(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Nop) { Label = instruction.Label});
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                Bytecode.BasicInstruction basic = new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Breakpoint)
                {
                    Label = instruction.Label
                };
                Result.Add(basic);
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

            //public override void Visit(FilterInstruction instruction)
            //{
            //    throw new NotImplementedException();
            //}

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
                if (instruction.HasOperand)
                    throw new NotImplementedException();

                Result.Add(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Return) { Label = instruction.Label});
            }

            public override void Visit(ThrowInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>()
                {
                    LoadOperand(instruction.Operand),
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Throw)
                };
                AddWithLabel(instructions, instruction.Label);
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
            /*public override void Visit(Bytecode.BranchInstruction op)
            {
                switch (op.Operation)
                {
                    case Bytecode.BranchOperation.False:
                    case Bytecode.BranchOperation.True:
                        ProcessUnaryConditionalBranch(op);
                        break;

                    case Bytecode.BranchOperation.Eq:
                    case Bytecode.BranchOperation.Neq:
                    case Bytecode.BranchOperation.Lt:
                    case Bytecode.BranchOperation.Le:
                    case Bytecode.BranchOperation.Gt:
                    case Bytecode.BranchOperation.Ge:
                        ProcessBinaryConditionalBranch(op);
                        break;

                    case Bytecode.BranchOperation.Branch:
                        ProcessUnconditionalBranch(op);
                        break;

                    case Bytecode.BranchOperation.Leave:
                        ProcessLeave(op);
                        break;

                    default: throw op.Operation.ToUnknownValueException();
                }
            }*/

                throw new NotImplementedException();
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(SwitchInstruction instruction)
            {
                //LoadOperand(instruction.Operand);
                //new Bytecode.SwitchInstruction(0, instruction.Targets);
                throw new NotImplementedException();
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>() { new Bytecode.SizeofInstruction(0, instruction.MeasuredType), StoreOperand(instruction.Result)};
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>() { new Bytecode.LoadTokenInstruction(0, instruction.Token), StoreOperand(instruction.Result)};
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                Model.Bytecode.MethodCallOperation op;
                switch (instruction.Operation)
                {
                    case MethodCallOperation.Jump:
                        op = Bytecode.MethodCallOperation.Jump;
                        break;
                    case MethodCallOperation.Static:
                        op = Bytecode.MethodCallOperation.Static;
                        break;
                    case MethodCallOperation.Virtual:
                        op = Bytecode.MethodCallOperation.Virtual;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (instruction.HasResult)
                    throw new NotImplementedException();

                var instructions = instruction.Arguments.Select(arg => LoadOperand(arg)).ToList();
                instructions.Add(new Bytecode.MethodCallInstruction(0, op, instruction.Method));
                AddWithLabel(instructions, instruction.Label);
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
                var instructions = new List<Bytecode.Instruction>()
                {
                    LoadOperand(instruction.TargetAddress),
                    LoadOperand(instruction.SourceAddress),
                    LoadOperand(instruction.NumberOfBytes),
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.CopyBlock),
                };

                AddWithLabel(instructions, instruction.Label);
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
