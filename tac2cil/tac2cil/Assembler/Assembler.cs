using Backend.Analyses;
using Backend.Model;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Bytecode = Model.Bytecode;

namespace tac2cil.Assembler
{
    public class Assembler
    {
        private readonly MethodBody _tacBody;

        public Assembler(MethodBody b)
        {
            Contract.Assert(b.Kind == MethodBodyKind.ThreeAddressCode);
            _tacBody = b;
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
                InstructionConverter instructionConverter = new InstructionConverter();
                ControlFlowAnalysis cfanalysis = new ControlFlowAnalysis(_tacBody);
                // exceptions disabled for now
                ControlFlowGraph cfg = cfanalysis.GenerateNormalControlFlow();

                //FillExceptionHandlersStart();
                foreach (CFGNode node in cfg.ForwardOrder)
                    ProcessBasicBlock(bytecodeBody, node, instructionConverter);

                bytecodeBody.Instructions.AddRange(instructionConverter.Result);
            }

            return bytecodeBody;
        }

        private void EmptyStackBeforeExit(MethodBody methodBody, int sizeAtExit)
        {
            Contract.Assert(methodBody.Kind.Equals(MethodBodyKind.Bytecode));
            Contract.Assert(methodBody.Instructions.All(i => i.Offset == 0));

            // all return instructions have the same label and offset, therefore they are all equal between them
            // we look for the indexes of their appearences
            IEnumerable<int> returnIndexes = methodBody.Instructions.Select((ins, indx) => ins is Bytecode.BasicInstruction basic &&
                                                                                    basic.Operation.Equals(Bytecode.BasicOperation.Return)
                                                                                    ? indx : -1)
                                                                                    .Where(idx => idx >= 0);

            // number of pops that we must execute before returning
            IEnumerable<Bytecode.BasicInstruction> pops = Enumerable.Repeat(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Pop), sizeAtExit);

            for (int i = 0; i < returnIndexes.Count(); i++)
            {
                foreach (Bytecode.BasicInstruction pop in pops)
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
            if (node.Instructions.Count == 0)
            {
                return;
            }

            IInstruction firstInstruction = node.Instructions.First();
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
            // A basic block can end with a non-branch instruction or a conditional branch instruction.
            // In those cases there is an implict flow, it is not encoded in any instruction.
            // That forces us to translate the nodes in a very specific order or translate each instruction linearly.
            // The implicit flow expects a particular instruction after another one.
            // In order to avoid that situation we make the control flow explicit.
            // If a basic block terminates with a non branch instruction, 
            // we generate a unconditional branch to the original fall through target.
            // If a basic block terminates with a conditional branch, we create an unconditional branch to the implicit target.
            private void TranslateImplicitFlow(IInstructionContainer container)
            {
                CFGNode n = container as CFGNode;

                var last = n.Instructions.Last();
                if (Model.Extensions.CanFallThroughNextInstruction(last))
                {
                    UnconditionalBranchInstruction explicitBr;
                    if (last is ConditionalBranchInstruction conditionalBranch)
                    {
                        var successor = n.Successors.Where(s => s.Instructions.First().Label != conditionalBranch.Target).Single();
                        explicitBr = new UnconditionalBranchInstruction(0, successor.Instructions.First().Label);
                    }
                    else
                    {
                        var successor = n.Successors.Single();
                        explicitBr = new UnconditionalBranchInstruction(0, successor.Instructions.First().Label);
                    }

                    explicitBr.Accept(this);
                }
            }
            public override void Visit(IInstructionContainer container)
            {
                for (int i=0; i < container.Instructions.Count;)
                {
                    Instruction current = container.Instructions[i] as Instruction;

                    // hacky?
                    // create FakeObjectCreationInstruction
                    // this is a workaround because analysis-net splits 
                    // Bytecode.CreatObjectInstruction into multiple instructions
                    // we need information from these three instructions to create again a
                    // Bytecode.CreatObjectInstruction
                    if (i + 2 < container.Instructions.Count &&
                        current is CreateObjectInstruction createObjectInstruction &&
                        container.Instructions[i + 1] is MethodCallInstruction callInstruction)
                    {
                        current = new FakeCreateObjectInstruction()
                        {
                            CreateObjectInstruction = createObjectInstruction,
                            MethodCallInstruction = callInstruction,
                        };

                        // do not process twice
                        // the same instructions
                        i += 2;
                    }
                    else
                        i += 1;

                    current.Accept(this);
                }

                TranslateImplicitFlow(container);
            }
            public class FakeCreateObjectInstruction : Instruction
            {
                public FakeCreateObjectInstruction() : base(0) {}

                public CreateObjectInstruction CreateObjectInstruction;
                public MethodCallInstruction MethodCallInstruction;

                public override void Accept(IInstructionVisitor visitor)
                {
                    var converter = visitor as InstructionConverter;
                    base.Accept(converter);
                    converter.Visit(this);
                }
            }

            public IList<Bytecode.Instruction> Result { get; private set; }
                = new List<Bytecode.Instruction>();
            //private readonly OperandStack _stack;
            private void AddWithLabel(IEnumerable<Bytecode.Instruction> instructions, string label)
            {
                instructions.First().Label = label;
                Result.AddRange(instructions);
            }

            public InstructionConverter()
            {
                //_stack = stack;
            }

            private Bytecode.Instruction Push(Constant constant)
            {
                // in bytecode there is no bool type
                if (constant.Value is Boolean b)
                    constant = new Constant(b == true ? 1 : 0);

                //_stack.Push();
                Bytecode.LoadInstruction l = new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Value, constant);
                return l;
            }

            private Bytecode.Instruction Push(IVariable variable, bool isAddress = false)
            {
                //_stack.Push();
                Bytecode.LoadInstruction l = new Bytecode.LoadInstruction(0, isAddress ? Bytecode.LoadOperation.Address : Bytecode.LoadOperation.Content, variable);
                return l;
            }
            private Bytecode.Instruction Pop(IVariable Result)
            {
                //_stack.Pop();
                Bytecode.StoreInstruction s = new Bytecode.StoreInstruction(0, Result);
                return s;
            }

            public void Visit(FakeCreateObjectInstruction instruction)
            {
                // skip 'this'
                var args = instruction.MethodCallInstruction.Arguments.Skip(1);
                var instructions = args.Select(arg => Push(arg)).ToList();
                instructions.Add(new Bytecode.CreateObjectInstruction(0, instruction.MethodCallInstruction.Method));
                instructions.Add(Pop(instruction.CreateObjectInstruction.Result));
                AddWithLabel(instructions, instruction.CreateObjectInstruction.Label);
            }

            public override void Visit(BinaryInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>
                {
                    Push(instruction.LeftOperand),
                    Push(instruction.RightOperand)
                };
                Bytecode.BasicInstruction basic = new Bytecode.BasicInstruction(0, instruction.Operation.ToBasicOperation())
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands,
                };
                instructions.Add(basic);
                instructions.Add(Pop(instruction.Result));
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>
                {
                    Push(instruction.Operand)
                };
                Bytecode.BasicInstruction basic = new Bytecode.BasicInstruction(0, instruction.Operation.ToUnaryOperation());
                instructions.Add(basic);
                instructions.Add(Pop(instruction.Result));
                AddWithLabel(instructions, instruction.Label);
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, Constant constant)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(constant));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, IVariable variable, bool isReference)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(variable, isReference));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, ArrayLengthAccess arrayLengthAccess)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(arrayLengthAccess.Instance));
                instructions.Add(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.LoadArrayLength));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, Bytecode.LoadArrayElementOperation op, ArrayElementAccess arrayElementAccess)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(arrayElementAccess.Array));
                foreach (var index in arrayElementAccess.Indices)
                    instructions.Add(Push(index));
                instructions.Add(new Bytecode.LoadArrayElementInstruction(0, op, (ArrayType)arrayElementAccess.Array.Type));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, Bytecode.LoadFieldOperation op, StaticFieldAccess staticFieldAccess)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(new Bytecode.LoadFieldInstruction(0, op, staticFieldAccess.Field));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, Bytecode.LoadFieldOperation op, InstanceFieldAccess instanceFieldAccess)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(instanceFieldAccess.Instance));
                instructions.Add(new Bytecode.LoadFieldInstruction(0, op, instanceFieldAccess.Field));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, VirtualMethodReference virtualMethodRef)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(virtualMethodRef.Instance));
                instructions.Add(new Bytecode.LoadMethodAddressInstruction(0, Bytecode.LoadMethodAddressOperation.Virtual, virtualMethodRef.Method));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, StaticMethodReference staticMethodRef)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(new Bytecode.LoadMethodAddressInstruction(0, Bytecode.LoadMethodAddressOperation.Static, staticMethodRef.Method));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IVariable result, Dereference dereference)
            {
                var instructions = new List<Bytecode.Instruction>();
                instructions.Add(Push(dereference.Reference, false));
                instructions.Add(new Bytecode.LoadIndirectInstruction(0, dereference.Type));
                instructions.Add(Pop(result));
                return instructions;
            }
            public IList<Bytecode.Instruction> ProcessLoad(IValue operand, IVariable result, bool isReference)
            {
                IList<Bytecode.Instruction> instructions;
                if (operand is Constant constant)
                {
                    instructions = ProcessLoad(result, constant);
                } else if (operand is IVariable variable)
                {
                    instructions = ProcessLoad(result, variable, isReference);
                }
                else if (operand is ArrayLengthAccess arrayLengthAccess)
                {
                    instructions = ProcessLoad(result, arrayLengthAccess);
                }
                else if (operand is ArrayElementAccess arrayElementAccess)
                {
                    var op = !isReference ? Bytecode.LoadArrayElementOperation.Content : Bytecode.LoadArrayElementOperation.Address;
                    instructions = ProcessLoad(result, op, arrayElementAccess);
                }
                else if (operand is Reference reference)
                {
                    instructions = ProcessLoad(reference.Value, result, true);
                }
                else if (operand is StaticFieldAccess staticFieldAccess)
                {
                    var op = !isReference ? Bytecode.LoadFieldOperation.Content : Bytecode.LoadFieldOperation.Address;
                    instructions = ProcessLoad(result, op, staticFieldAccess);
                }
                else if (operand is InstanceFieldAccess instanceFieldAccess)
                {
                    var op = !isReference ? Bytecode.LoadFieldOperation.Content : Bytecode.LoadFieldOperation.Address;
                    instructions = ProcessLoad(result, op, instanceFieldAccess);
                }
                else if (operand is Dereference dereference)
                {
                    instructions = ProcessLoad(result, dereference);
                }
                else if (operand is StaticMethodReference staticMethodRef)
                {
                    instructions = ProcessLoad(result, staticMethodRef);
                }
                else if (operand is VirtualMethodReference virtualMethodRef)
                {
                    instructions = ProcessLoad(result, virtualMethodRef);
                }
                else
                    throw new NotImplementedException();

                return instructions;
            }

            public override void Visit(LoadInstruction instruction)
            {
                IList<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>();
                instructions = ProcessLoad(instruction.Operand, instruction.Result, instruction.Operand is Reference);
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(StoreInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>();

                if (instruction.Result is ArrayElementAccess arrayElementAccess)
                {
                    instructions.Add(Push(arrayElementAccess.Array));
                    foreach (var index in arrayElementAccess.Indices)
                    {
                        instructions.Add(Push(index));
                    }
                    instructions.Add(Push(instruction.Operand));
                    
                    instructions.Add(new Bytecode.StoreArrayElementInstruction(0, (ArrayType)arrayElementAccess.Array.Type));
                }
                else if (instruction.Result is StaticFieldAccess staticFieldAccess)
                {
                    instructions.Add(Push(instruction.Operand));
                    instructions.Add(new Bytecode.StoreFieldInstruction(0, staticFieldAccess.Field));
                }
                else if (instruction.Result is InstanceFieldAccess instanceFieldAccess)
                {
                    instructions.Add(Push(instanceFieldAccess.Instance));
                    instructions.Add(Push(instruction.Operand));
                    instructions.Add(new Bytecode.StoreFieldInstruction(0, instanceFieldAccess.Field));
                }
                else if (instruction.Result is Dereference dereference)
                {
                    instructions.Add(Push(dereference.Reference));
                    instructions.Add(Push(instruction.Operand));
                    var type = dereference.Type.Equals(PlatformTypes.Boolean) ? PlatformTypes.SByte : dereference.Type;

                    instructions.Add(new Bytecode.StoreIndirectInstruction(0, type));
                }
                else
                    throw new NotImplementedException();

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(NopInstruction instruction)
            {
                Result.Add(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Nop) { Label = instruction.Label });
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
                //throw new NotImplementedException();
            }

            public override void Visit(FaultInstruction instruction)
            {
                //throw new NotImplementedException();
            }

            public override void Visit(FinallyInstruction instruction)
            {
                //throw new NotImplementedException();
            }

            //public override void Visit(FilterInstruction instruction)
            //{
            //    throw new NotImplementedException();
            //}

            public override void Visit(CatchInstruction instruction)
            {
                //throw new NotImplementedException();
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>();

                var operation = instruction.Operation.ToConvertOperation();
                instructions.Add(Push(instruction.Operand));
                instructions.Add(new Bytecode.ConvertInstruction(0, operation, instruction.ConversionType));
                instructions.Add(Pop(instruction.Result));

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(ReturnInstruction instruction)
            {
                var instructions = new List<Bytecode.Instruction>();

                if (instruction.HasOperand)
                {
                    instructions.Add(Push(instruction.Operand));
                }

                instructions.Add(new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Return));
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(ThrowInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>()
                {
                    Push(instruction.Operand),
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Throw)
                };
                AddWithLabel(instructions, instruction.Label);
            }

            // this is an abstract class 
            // we have implemented a visitor for each specialization
            /*public override void Visit(BranchInstruction instruction)
            {
                throw new NotImplementedException();
            }*/

            public override void Visit(ExceptionalBranchInstruction instruction)
            {
                throw new NotImplementedException();
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                var br = new Bytecode.BranchInstruction(0, Bytecode.BranchOperation.Branch, 0);
                br.Target = instruction.Target;
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>() { br };
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var left = Push(instruction.LeftOperand);
                Bytecode.Instruction right = null;
                if (instruction.RightOperand is Constant rightConstant)
                {
                    right = Push(rightConstant);
                }
                else if (instruction.RightOperand is IVariable rightVariable)
                {
                    right = Push(rightVariable);
                }
                else if (instruction.RightOperand is UnknownValue)
                    throw new NotImplementedException();
                    

                var br = new Bytecode.BranchInstruction(0, instruction.Operation.ToBranchOperation(), 0);
                br.Target = instruction.Target;
                br.UnsignedOperands = instruction.UnsignedOperands;

                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>() { left, right, br };
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var op = Push(instruction.Operand);
                var sw = new Bytecode.SwitchInstruction(0, new uint[0]);
                sw.Targets.AddRange(instruction.Targets);

                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>() { op, sw };
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>() { new Bytecode.SizeofInstruction(0, instruction.MeasuredType), Pop(instruction.Result) };
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>() { new Bytecode.LoadTokenInstruction(0, instruction.Token), Pop(instruction.Result) };
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

                List<Bytecode.Instruction> instructions = instruction.Arguments.Select(arg => Push(arg)).ToList();

                if (op == Bytecode.MethodCallOperation.Virtual &&
                    instruction.Arguments.First().Type is PointerType ptrType &&
                    ptrType.TargetType is IGenericParameterReference)
                    instructions.Add(new Bytecode.ConstrainedInstruction(0, ptrType.TargetType));

                instructions.Add(new Bytecode.MethodCallInstruction(0, op, instruction.Method));

                if (instruction.HasResult)
                    instructions.Add(Pop(instruction.Result));

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = instruction.Arguments.Select(arg => Push(arg)).ToList();
                instructions.Add(Push(instruction.Pointer));

                instructions.Add(new Bytecode.IndirectMethodCallInstruction(0, (FunctionPointerType)instruction.Pointer.Type));

                if (instruction.HasResult)
                    instructions.Add(Pop(instruction.Result));

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                // this is done in Visit(FakeCreateObjectInstruction)
                throw new NotSupportedException();
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>()
                {
                    Push(instruction.TargetAddress),
                    Push(instruction.SourceAddress),
                    Push(instruction.NumberOfBytes),
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.CopyBlock),
                };

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>()
                {
                    Push(instruction.NumberOfBytes),
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.LocalAllocation),
                };

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>()
                {
                    Push(instruction.TargetAddress, true),
                    Push(instruction.Value),
                    Push(instruction.NumberOfBytes),
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.InitBlock),
                };

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>()
                {
                    Push(instruction.TargetAddress, false),
                    // not sure if TargetAddress returns ptr<type> or type
                    new Bytecode.InitObjInstruction(0, (instruction.TargetAddress.Type as PointerType).TargetType),
                };
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>()
                {
                    Push(instruction.TargetAddress, true),
                    Push(instruction.SourceAddress, true),

                    // not sure if TargetAddress returns ptr<type> or type
                    new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.CopyObject),
                };
                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                List<Bytecode.Instruction> instructions = new List<Bytecode.Instruction>();
                foreach (var lb in instruction.LowerBounds)
                    instructions.Add(Push(lb));
                foreach (var s in instruction.Sizes)
                    instructions.Add(Push(s));
                // im assuming the result is an array
                instructions.Add(new Bytecode.CreateArrayInstruction(0, (ArrayType)instruction.Result.Type));
                instructions.Add(Pop(instruction.Result));

                AddWithLabel(instructions, instruction.Label);
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new NotImplementedException();
            }
        }
    }
}
