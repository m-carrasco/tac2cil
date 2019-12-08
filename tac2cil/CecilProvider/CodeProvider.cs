using System;
using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using AnalysisNetBytecode = Model.Bytecode;
using AnalysisNetTac = Model.ThreeAddressCode;
using Cecil = Mono.Cecil;

namespace CecilProvider
{
    internal struct FakeArrayType : AnalysisNet.Types.IBasicType
    {
        public AnalysisNet.Types.ArrayType Type { get; private set; }

        public FakeArrayType(AnalysisNet.Types.ArrayType type)
        {
            Type = type;
        }

        public AnalysisNet.IAssemblyReference ContainingAssembly => null;

        public string ContainingNamespace => string.Empty;

        public string Name => "FakeArray";

        public string GenericName => Name;

        public IList<AnalysisNet.Types.IType> GenericArguments => null;

        public AnalysisNet.Types.IBasicType GenericType => null;

        public AnalysisNet.Types.TypeDefinition ResolvedType => null;

        public AnalysisNet.Types.TypeKind TypeKind => AnalysisNet.Types.TypeKind.ReferenceType;

        public ISet<AnalysisNet.Types.CustomAttribute> Attributes => null;

        public int GenericParameterCount => 0;

        public AnalysisNet.Types.IBasicType ContainingType => null;
    }

    internal class CodeProvider
    {
        private readonly TypeExtractor typeExtractor;
        private readonly IDictionary<int, AnalysisNetTac.Values.IVariable> parameters;
        private readonly IDictionary<int, AnalysisNetTac.Values.IVariable> locals;
        private AnalysisNetTac.Values.IVariable thisParameter;

        private Cecil.Cil.MethodBody cecilBody;
        public CodeProvider(TypeExtractor typeExtractor)
        {
            this.typeExtractor = typeExtractor;
            parameters = new Dictionary<int, AnalysisNetTac.Values.IVariable>();
            locals = new Dictionary<int, AnalysisNetTac.Values.IVariable>();
        }

        public AnalysisNet.Types.MethodBody ExtractBody(Cecil.Cil.MethodBody cciBody)
        {
            cecilBody = cciBody;
            AnalysisNet.Types.MethodBody ourBody = new AnalysisNet.Types.MethodBody(AnalysisNet.Types.MethodBodyKind.Bytecode)
            {
                MaxStack = (ushort)cciBody.MaxStackSize
            };
            ExtractParameters(cciBody.Method, ourBody.Parameters);
            ExtractLocalVariables(cciBody.Variables, ourBody.LocalVariables);
            ExtractExceptionInformation(cciBody.ExceptionHandlers, ourBody.ExceptionInformation);
            ExtractInstructions(cciBody.Instructions, ourBody.Instructions);

            return ourBody;
        }

        private void ExtractParameters(Cecil.MethodDefinition methoddef, IList<AnalysisNetTac.Values.IVariable> ourParameters)
        {
            if (!methoddef.IsStatic)
            {
                AnalysisNet.Types.IType type = typeExtractor.ExtractType(methoddef.DeclaringType);
                AnalysisNetTac.Values.LocalVariable v = new AnalysisNetTac.Values.LocalVariable("this", true) { Type = type };

                ourParameters.Add(v);
                thisParameter = v;
            }

            foreach (Cecil.ParameterDefinition parameter in methoddef.Parameters)
            {
                AnalysisNet.Types.IType type = typeExtractor.ExtractType(parameter.ParameterType);
                AnalysisNetTac.Values.LocalVariable v = new AnalysisNetTac.Values.LocalVariable(parameter.Name, true) { Type = type };

                ourParameters.Add(v);
                parameters.Add(parameter.Index, v);
            }
        }

        private void ExtractLocalVariables(IEnumerable<Cecil.Cil.VariableDefinition> cciLocalVariables, IList<AnalysisNetTac.Values.IVariable> ourLocalVariables)
        {
            foreach (Cecil.Cil.VariableDefinition local in cciLocalVariables)
            {
                string name = GetLocalSourceName(local);
                AnalysisNet.Types.IType type = typeExtractor.ExtractType(local.VariableType);
                AnalysisNetTac.Values.LocalVariable v = new AnalysisNetTac.Values.LocalVariable(name) { Type = type };

                ourLocalVariables.Add(v);
                locals.Add(local.Index, v);
            }
        }

        private void ExtractExceptionInformation(IEnumerable<Cecil.Cil.ExceptionHandler> cciExceptionInformation, IList<AnalysisNet.ProtectedBlock> ourExceptionInformation)
        {
            foreach (Cecil.Cil.ExceptionHandler cciExceptionInfo in cciExceptionInformation)
            {
                AnalysisNet.ProtectedBlock tryHandler = new AnalysisNet.ProtectedBlock((uint)cciExceptionInfo.TryStart.Offset, (uint)cciExceptionInfo.TryEnd.Offset);

                switch (cciExceptionInfo.HandlerType)
                {
                    case Cecil.Cil.ExceptionHandlerType.Filter:
                        AnalysisNet.Types.IType filterExceptionType = typeExtractor.ExtractType((Cecil.TypeReference)cciExceptionInfo.FilterStart.Operand);
                        AnalysisNet.FilterExceptionHandler filterHandler = new AnalysisNet.FilterExceptionHandler((uint)cciExceptionInfo.FilterStart.Offset, (uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset, filterExceptionType);
                        tryHandler.Handler = filterHandler;
                        break;

                    case Cecil.Cil.ExceptionHandlerType.Catch:
                        AnalysisNet.Types.IType catchExceptionType = typeExtractor.ExtractType(cciExceptionInfo.CatchType);
                        AnalysisNet.CatchExceptionHandler catchHandler = new AnalysisNet.CatchExceptionHandler((uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset, catchExceptionType);
                        tryHandler.Handler = catchHandler;
                        break;

                    case Cecil.Cil.ExceptionHandlerType.Fault:
                        AnalysisNet.FaultExceptionHandler faultHandler = new AnalysisNet.FaultExceptionHandler((uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset);
                        tryHandler.Handler = faultHandler;
                        break;

                    case Cecil.Cil.ExceptionHandlerType.Finally:
                        AnalysisNet.FinallyExceptionHandler finallyHandler = new AnalysisNet.FinallyExceptionHandler((uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset);
                        tryHandler.Handler = finallyHandler;
                        break;

                    default:
                        throw new Exception("Unknown exception handler block kind");
                }

                ourExceptionInformation.Add(tryHandler);
            }
        }

        private void ExtractInstructions(IEnumerable<Cecil.Cil.Instruction> operations, IList<AnalysisNet.IInstruction> instructions)
        {
            foreach (Cecil.Cil.Instruction op in operations)
            {
                AnalysisNet.IInstruction instruction = ExtractInstruction(op);
                instructions.Add(instruction);
            }
        }

        private AnalysisNet.IInstruction ExtractInstruction(Cecil.Cil.Instruction operation)
        {
            AnalysisNet.IInstruction instruction = null;

            // cecil does not have an enum we require it for the switch statement
            Cecil.Cil.Code code = operation.OpCode.Code;
            switch (code)
            {
                case Mono.Cecil.Cil.Code.Add:
                case Mono.Cecil.Cil.Code.Add_Ovf:
                case Mono.Cecil.Cil.Code.Add_Ovf_Un:
                case Mono.Cecil.Cil.Code.And:
                case Mono.Cecil.Cil.Code.Ceq:
                case Mono.Cecil.Cil.Code.Cgt:
                case Mono.Cecil.Cil.Code.Cgt_Un:
                case Mono.Cecil.Cil.Code.Clt:
                case Mono.Cecil.Cil.Code.Clt_Un:
                case Mono.Cecil.Cil.Code.Div:
                case Mono.Cecil.Cil.Code.Div_Un:
                case Mono.Cecil.Cil.Code.Mul:
                case Mono.Cecil.Cil.Code.Mul_Ovf:
                case Mono.Cecil.Cil.Code.Mul_Ovf_Un:
                case Mono.Cecil.Cil.Code.Or:
                case Mono.Cecil.Cil.Code.Rem:
                case Mono.Cecil.Cil.Code.Rem_Un:
                case Mono.Cecil.Cil.Code.Shl:
                case Mono.Cecil.Cil.Code.Shr:
                case Mono.Cecil.Cil.Code.Shr_Un:
                case Mono.Cecil.Cil.Code.Sub:
                case Mono.Cecil.Cil.Code.Sub_Ovf:
                case Mono.Cecil.Cil.Code.Sub_Ovf_Un:
                case Mono.Cecil.Cil.Code.Xor:
                    instruction = ProcessBasic(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Arglist:
                //    //expression = new RuntimeArgumentHandleExpression();
                //    break;

                //case Mono.Cecil.Cil.Code.Array_Create_WithLowerBound:
                //case Mono.Cecil.Cil.Code.Array_Create:
                case Mono.Cecil.Cil.Code.Newarr:
                    instruction = ProcessCreateArray(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Array_Get:
                //    instruction = ProcessLoadArrayElement(operation, AnalysisNetBytecode.LoadArrayElementOperation.Content);
                //    break;
                //case Mono.Cecil.Cil.Code.Array_Addr:
                //    instruction = ProcessLoadArrayElement(operation, AnalysisNetBytecode.LoadArrayElementOperation.Address);
                //    break;

                //case Mono.Cecil.Cil.Code.Ldelem:
                case Mono.Cecil.Cil.Code.Ldelem_Any:
                case Mono.Cecil.Cil.Code.Ldelem_I:
                case Mono.Cecil.Cil.Code.Ldelem_I1:
                case Mono.Cecil.Cil.Code.Ldelem_I2:
                case Mono.Cecil.Cil.Code.Ldelem_I4:
                case Mono.Cecil.Cil.Code.Ldelem_I8:
                case Mono.Cecil.Cil.Code.Ldelem_R4:
                case Mono.Cecil.Cil.Code.Ldelem_R8:
                case Mono.Cecil.Cil.Code.Ldelem_U1:
                case Mono.Cecil.Cil.Code.Ldelem_U2:
                case Mono.Cecil.Cil.Code.Ldelem_U4:
                case Mono.Cecil.Cil.Code.Ldelem_Ref:
                    instruction = ProcessLoadArrayElement(operation, AnalysisNetBytecode.LoadArrayElementOperation.Content);
                    break;

                case Mono.Cecil.Cil.Code.Ldelema:
                    instruction = ProcessLoadArrayElement(operation, AnalysisNetBytecode.LoadArrayElementOperation.Address);
                    break;

                case Mono.Cecil.Cil.Code.Beq:
                case Mono.Cecil.Cil.Code.Beq_S:
                case Mono.Cecil.Cil.Code.Bne_Un:
                case Mono.Cecil.Cil.Code.Bne_Un_S:
                case Mono.Cecil.Cil.Code.Bge:
                case Mono.Cecil.Cil.Code.Bge_S:
                case Mono.Cecil.Cil.Code.Bge_Un:
                case Mono.Cecil.Cil.Code.Bge_Un_S:
                case Mono.Cecil.Cil.Code.Bgt:
                case Mono.Cecil.Cil.Code.Bgt_S:
                case Mono.Cecil.Cil.Code.Bgt_Un:
                case Mono.Cecil.Cil.Code.Bgt_Un_S:
                case Mono.Cecil.Cil.Code.Ble:
                case Mono.Cecil.Cil.Code.Ble_S:
                case Mono.Cecil.Cil.Code.Ble_Un:
                case Mono.Cecil.Cil.Code.Ble_Un_S:
                case Mono.Cecil.Cil.Code.Blt:
                case Mono.Cecil.Cil.Code.Blt_S:
                case Mono.Cecil.Cil.Code.Blt_Un:
                case Mono.Cecil.Cil.Code.Blt_Un_S:
                    instruction = ProcessBinaryConditionalBranch(operation);
                    break;

                case Mono.Cecil.Cil.Code.Br:
                case Mono.Cecil.Cil.Code.Br_S:
                    instruction = ProcessUnconditionalBranch(operation);
                    break;

                case Mono.Cecil.Cil.Code.Leave:
                case Mono.Cecil.Cil.Code.Leave_S:
                    instruction = ProcessLeave(operation);
                    break;

                case Mono.Cecil.Cil.Code.Break:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Nop:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Brfalse:
                case Mono.Cecil.Cil.Code.Brfalse_S:
                case Mono.Cecil.Cil.Code.Brtrue:
                case Mono.Cecil.Cil.Code.Brtrue_S:
                    instruction = ProcessUnaryConditionalBranch(operation);
                    break;

                case Mono.Cecil.Cil.Code.Call:
                case Mono.Cecil.Cil.Code.Callvirt:
                case Mono.Cecil.Cil.Code.Jmp:
                    instruction = ProcessMethodCall(operation);
                    break;

                case Mono.Cecil.Cil.Code.Calli:
                    instruction = ProcessMethodCallIndirect(operation);
                    break;

                case Mono.Cecil.Cil.Code.Castclass:
                case Mono.Cecil.Cil.Code.Isinst:
                case Mono.Cecil.Cil.Code.Box:
                case Mono.Cecil.Cil.Code.Unbox:
                case Mono.Cecil.Cil.Code.Unbox_Any:
                case Mono.Cecil.Cil.Code.Conv_I:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I_Un:
                case Mono.Cecil.Cil.Code.Conv_I1:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I1:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I1_Un:
                case Mono.Cecil.Cil.Code.Conv_I2:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I2:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I2_Un:
                case Mono.Cecil.Cil.Code.Conv_I4:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I4:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I4_Un:
                case Mono.Cecil.Cil.Code.Conv_I8:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I8:
                case Mono.Cecil.Cil.Code.Conv_Ovf_I8_Un:
                case Mono.Cecil.Cil.Code.Conv_U:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U_Un:
                case Mono.Cecil.Cil.Code.Conv_U1:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U1:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U1_Un:
                case Mono.Cecil.Cil.Code.Conv_U2:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U2:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U2_Un:
                case Mono.Cecil.Cil.Code.Conv_U4:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U4:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U4_Un:
                case Mono.Cecil.Cil.Code.Conv_U8:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U8:
                case Mono.Cecil.Cil.Code.Conv_Ovf_U8_Un:
                case Mono.Cecil.Cil.Code.Conv_R4:
                case Mono.Cecil.Cil.Code.Conv_R8:
                case Mono.Cecil.Cil.Code.Conv_R_Un:
                    instruction = ProcessConversion(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Ckfinite:
                //    var operand = result = PopOperandStack();
                //    var chkfinite = new MutableCodeModel.MethodReference()
                //    {
                //        CallingConvention = Cci.CallingConvention.FastCall,
                //        ContainingType = host.PlatformType.SystemFloat64,
                //        Name = result = host.NameTable.GetNameFor("__ckfinite__"),
                //        Type = host.PlatformType.SystemFloat64,
                //        InternFactory = host.InternFactory,
                //    };
                //    expression = new MethodCall() { Arguments = new List<IExpression>(1) { operand }, IsStaticCall = true, Type = operand.Type, MethodToCall = chkfinite };
                //    break;

                case Mono.Cecil.Cil.Code.Constrained:
                    instruction = ProcessConstrained(operation);
                    break;

                case Mono.Cecil.Cil.Code.Cpblk:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Cpobj:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Dup:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Endfilter:
                case Mono.Cecil.Cil.Code.Endfinally:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Initblk:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Initobj:
                    instruction = ProcessInitObj(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldarg:
                case Mono.Cecil.Cil.Code.Ldarg_0:
                case Mono.Cecil.Cil.Code.Ldarg_1:
                case Mono.Cecil.Cil.Code.Ldarg_2:
                case Mono.Cecil.Cil.Code.Ldarg_3:
                case Mono.Cecil.Cil.Code.Ldarg_S:
                case Mono.Cecil.Cil.Code.Ldarga:
                case Mono.Cecil.Cil.Code.Ldarga_S:
                    instruction = ProcessLoadArgument(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldloc:
                case Mono.Cecil.Cil.Code.Ldloc_0:
                case Mono.Cecil.Cil.Code.Ldloc_1:
                case Mono.Cecil.Cil.Code.Ldloc_2:
                case Mono.Cecil.Cil.Code.Ldloc_3:
                case Mono.Cecil.Cil.Code.Ldloc_S:
                case Mono.Cecil.Cil.Code.Ldloca:
                case Mono.Cecil.Cil.Code.Ldloca_S:
                    instruction = ProcessLoadLocal(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldfld:
                case Mono.Cecil.Cil.Code.Ldsfld:
                case Mono.Cecil.Cil.Code.Ldflda:
                case Mono.Cecil.Cil.Code.Ldsflda:
                    instruction = ProcessLoadField(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldftn:
                case Mono.Cecil.Cil.Code.Ldvirtftn:
                    instruction = ProcessLoadMethodAddress(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldc_I4:
                case Mono.Cecil.Cil.Code.Ldc_I4_0:
                case Mono.Cecil.Cil.Code.Ldc_I4_1:
                case Mono.Cecil.Cil.Code.Ldc_I4_2:
                case Mono.Cecil.Cil.Code.Ldc_I4_3:
                case Mono.Cecil.Cil.Code.Ldc_I4_4:
                case Mono.Cecil.Cil.Code.Ldc_I4_5:
                case Mono.Cecil.Cil.Code.Ldc_I4_6:
                case Mono.Cecil.Cil.Code.Ldc_I4_7:
                case Mono.Cecil.Cil.Code.Ldc_I4_8:
                case Mono.Cecil.Cil.Code.Ldc_I4_M1:
                case Mono.Cecil.Cil.Code.Ldc_I4_S:
                case Mono.Cecil.Cil.Code.Ldc_I8:
                case Mono.Cecil.Cil.Code.Ldc_R4:
                case Mono.Cecil.Cil.Code.Ldc_R8:
                case Mono.Cecil.Cil.Code.Ldnull:
                case Mono.Cecil.Cil.Code.Ldstr:
                    instruction = ProcessLoadConstant(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldind_I:
                case Mono.Cecil.Cil.Code.Ldind_I1:
                case Mono.Cecil.Cil.Code.Ldind_I2:
                case Mono.Cecil.Cil.Code.Ldind_I4:
                case Mono.Cecil.Cil.Code.Ldind_I8:
                case Mono.Cecil.Cil.Code.Ldind_R4:
                case Mono.Cecil.Cil.Code.Ldind_R8:
                case Mono.Cecil.Cil.Code.Ldind_Ref:
                case Mono.Cecil.Cil.Code.Ldind_U1:
                case Mono.Cecil.Cil.Code.Ldind_U2:
                case Mono.Cecil.Cil.Code.Ldind_U4:
                case Mono.Cecil.Cil.Code.Ldobj:
                    instruction = ProcessLoadIndirect(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldlen:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Ldtoken:
                    instruction = ProcessLoadToken(operation);
                    break;

                case Mono.Cecil.Cil.Code.Localloc:
                    instruction = ProcessBasic(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Mkrefany:
                //    expression = result = ParseMakeTypedReference(currentOperation);
                //    break;

                case Mono.Cecil.Cil.Code.Neg:
                case Mono.Cecil.Cil.Code.Not:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Newobj:
                    instruction = ProcessCreateObject(operation);
                    break;

                //case Mono.Cecil.Cil.Code.No_:
                //    // If code out there actually uses this, I need to know sooner rather than later.
                //    // TODO: need object model support
                //    throw new NotImplementedException("Invalid opcode: No.");

                case Mono.Cecil.Cil.Code.Pop:
                    instruction = ProcessBasic(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Readonly_:
                //    result = sawReadonly = true;
                //    break;

                //case Mono.Cecil.Cil.Code.Refanytype:
                //    expression = result = ParseGetTypeOfTypedReference();
                //    break;

                //case Mono.Cecil.Cil.Code.Refanyval:
                //    expression = result = ParseGetValueOfTypedReference(currentOperation);
                //    break;

                case Mono.Cecil.Cil.Code.Ret:
                    instruction = ProcessBasic(operation);
                    break;

                case Mono.Cecil.Cil.Code.Sizeof:
                    instruction = ProcessSizeof(operation);
                    break;

                case Mono.Cecil.Cil.Code.Starg:
                case Mono.Cecil.Cil.Code.Starg_S:
                    instruction = ProcessStoreArgument(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Array_Set:
                //case Mono.Cecil.Cil.Code.Stelem:
                case Mono.Cecil.Cil.Code.Stelem_Any:
                case Mono.Cecil.Cil.Code.Stelem_I:
                case Mono.Cecil.Cil.Code.Stelem_I1:
                case Mono.Cecil.Cil.Code.Stelem_I2:
                case Mono.Cecil.Cil.Code.Stelem_I4:
                case Mono.Cecil.Cil.Code.Stelem_I8:
                case Mono.Cecil.Cil.Code.Stelem_R4:
                case Mono.Cecil.Cil.Code.Stelem_R8:
                case Mono.Cecil.Cil.Code.Stelem_Ref:
                    instruction = ProcessStoreArrayElement(operation);
                    break;

                case Mono.Cecil.Cil.Code.Stfld:
                case Mono.Cecil.Cil.Code.Stsfld:
                    instruction = ProcessStoreField(operation);
                    break;

                case Mono.Cecil.Cil.Code.Stind_I:
                case Mono.Cecil.Cil.Code.Stind_I1:
                case Mono.Cecil.Cil.Code.Stind_I2:
                case Mono.Cecil.Cil.Code.Stind_I4:
                case Mono.Cecil.Cil.Code.Stind_I8:
                case Mono.Cecil.Cil.Code.Stind_R4:
                case Mono.Cecil.Cil.Code.Stind_R8:
                case Mono.Cecil.Cil.Code.Stind_Ref:
                case Mono.Cecil.Cil.Code.Stobj:
                    instruction = ProcessStoreIndirect(operation);
                    break;

                case Mono.Cecil.Cil.Code.Stloc:
                case Mono.Cecil.Cil.Code.Stloc_0:
                case Mono.Cecil.Cil.Code.Stloc_1:
                case Mono.Cecil.Cil.Code.Stloc_2:
                case Mono.Cecil.Cil.Code.Stloc_3:
                case Mono.Cecil.Cil.Code.Stloc_S:
                    instruction = ProcessStoreLocal(operation);
                    break;

                case Mono.Cecil.Cil.Code.Switch:
                    instruction = ProcessSwitch(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Tail_:
                //    result = sawTailCall = true;
                //    break;

                case Mono.Cecil.Cil.Code.Throw:
                case Mono.Cecil.Cil.Code.Rethrow:
                    instruction = ProcessBasic(operation);
                    break;

                //case Mono.Cecil.Cil.Code.Unaligned_:
                //    Contract.Assume(currentOperation.Value is byte);
                //    var alignment = (byte)currentOperation.Value;
                //    Contract.Assume(alignment == 1 || alignment == 2 || alignment == 4);
                //    result = alignment = alignment;
                //    break;

                //case Mono.Cecil.Cil.Code.Volatile_:
                //    result = sawVolatile = true;
                //    break;

                default:
                    //Console.WriteLine("Unknown bytecode: {0}", operation.OperationCode);
                    //throw new UnknownBytecodeException(operation);
                    //continue;

                    // Quick fix to preserve the offset in case it is a target location of some jump
                    // Otherwise it will break the control-flow analysis later.
                    instruction = new AnalysisNetBytecode.BasicInstruction((uint)operation.Offset, AnalysisNetBytecode.BasicOperation.Nop);
                    break;
            }

            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadArrayElement(Cecil.Cil.Instruction op, AnalysisNetBytecode.LoadArrayElementOperation operation, AnalysisNet.Types.ArrayType arrayType = null)
        {
            AnalysisNetBytecode.LoadArrayElementInstruction instruction = new AnalysisNetBytecode.LoadArrayElementInstruction((uint)op.Offset, operation, arrayType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreArrayElement(Cecil.Cil.Instruction op, AnalysisNet.Types.ArrayType arrayType)
        {
            AnalysisNetBytecode.StoreArrayElementInstruction instruction = new AnalysisNetBytecode.StoreArrayElementInstruction((uint)op.Offset, arrayType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreArrayElement(Cecil.Cil.Instruction op)
        {
            AnalysisNet.Types.ArrayType arrayType = null;

            switch (op.OpCode.Code)
            {
                //case Mono.Cecil.Cil.Code.Array_Set:
                //    arrayType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference) as ArrayType;
                //    break;
                //case Mono.Cecil.Cil.Code.Stelem:
                case Mono.Cecil.Cil.Code.Stelem_Any:
                    AnalysisNet.Types.IType extractedType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
                    arrayType = new AnalysisNet.Types.ArrayType(extractedType);
                    break;
                default:
                    arrayType = new AnalysisNet.Types.ArrayType(OperationHelper.GetOperationType(op.OpCode.Code));
                    break;
            }

            if (arrayType == null)
            {
                throw new NotImplementedException();
            }

            AnalysisNetBytecode.StoreArrayElementInstruction instruction = new AnalysisNetBytecode.StoreArrayElementInstruction((uint)op.Offset, arrayType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadArrayElement(Cecil.Cil.Instruction op, AnalysisNetBytecode.LoadArrayElementOperation operation)
        {
            AnalysisNet.Types.ArrayType arrayType = null;

            switch (op.OpCode.Code)
            {
                /*case Mono.Cecil.Cil.Code.Array_Addr:
                case Mono.Cecil.Cil.Code.Array_Create:
                case Mono.Cecil.Cil.Code.Array_Create_WithLowerBound:
                case Mono.Cecil.Cil.Code.Array_Get:
                case Mono.Cecil.Cil.Code.Array_Set:
                    arrayType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference) as ArrayType;
                    break;*/
                //case Mono.Cecil.Cil.Code.Ldelem:
                case Mono.Cecil.Cil.Code.Ldelem_Any:
                case Mono.Cecil.Cil.Code.Ldelema:
                    arrayType = new AnalysisNet.Types.ArrayType(typeExtractor.ExtractType(op.Operand as Cecil.TypeReference));
                    break;
                default:
                    arrayType = new AnalysisNet.Types.ArrayType(OperationHelper.GetOperationType(op.OpCode.Code));
                    break;
            }

            if (arrayType == null)
            {
                throw new NotImplementedException();
            }

            AnalysisNetBytecode.LoadArrayElementInstruction instruction = new AnalysisNetBytecode.LoadArrayElementInstruction((uint)op.Offset, operation, arrayType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessConstrained(Cecil.Cil.Instruction op)
        {
            AnalysisNet.Types.IType thisType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            AnalysisNetBytecode.ConstrainedInstruction ins = new AnalysisNetBytecode.ConstrainedInstruction((uint)op.Offset, thisType);
            return ins;
        }

        private AnalysisNet.IInstruction ProcessSwitch(Cecil.Cil.Instruction op)
        {
            Cecil.Cil.Instruction[] targets = op.Operand as Cecil.Cil.Instruction[];

            AnalysisNetBytecode.SwitchInstruction instruction = new AnalysisNetBytecode.SwitchInstruction((uint)op.Offset, targets.Select(t => (uint)t.Offset));
            return instruction;
        }
        private AnalysisNet.IInstruction ProcessCreateArray(Cecil.Cil.Instruction op)
        {
            Cecil.ArrayType cciArrayType = Cecil.Rocks.TypeReferenceRocks.MakeArrayType(op.Operand as Cecil.TypeReference);
            AnalysisNet.Types.ArrayType ourArrayType = typeExtractor.ExtractType(cciArrayType) as AnalysisNet.Types.ArrayType;

            return CreateArray((uint)op.Offset, ourArrayType);
        }
        private AnalysisNet.IInstruction CreateArray(uint offset, AnalysisNet.Types.ArrayType arrayType, bool withLowerBound = false)
        {
            AnalysisNetBytecode.CreateArrayInstruction instruction = new AnalysisNetBytecode.CreateArrayInstruction(offset, arrayType)
            {
                WithLowerBound = withLowerBound
            };
            return instruction;
        }
        private AnalysisNet.IInstruction ProcessCreateObject(Cecil.Cil.Instruction op)
        {
            Cecil.MethodReference cciMethod = op.Operand as Cecil.MethodReference;
            AnalysisNet.Types.IMethodReference ourMethod = typeExtractor.ExtractMethod(cciMethod);

            if (ourMethod.ContainingType is FakeArrayType fakeArrayType)
            {
                bool withLowerBounds = ourMethod.Parameters.Count > fakeArrayType.Type.Rank;
                return CreateArray((uint)op.Offset, fakeArrayType.Type, withLowerBounds);
            }

            AnalysisNetBytecode.CreateObjectInstruction instruction = new AnalysisNetBytecode.CreateObjectInstruction((uint)op.Offset, ourMethod);
            return instruction;
        }
        private AnalysisNet.IInstruction ProcessMethodCall(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.MethodCallOperation operation = OperationHelper.ToMethodCallOperation(op.OpCode.Code);
            Cecil.MethodReference cciMethod = op.Operand as Cecil.MethodReference;
            AnalysisNet.Types.IMethodReference ourMethod = typeExtractor.ExtractMethod(cciMethod);

            AnalysisNet.IInstruction instruction;

            if (ourMethod.ContainingType is FakeArrayType fakeArrayType)
            {
                AnalysisNet.Types.ArrayType arrayType = fakeArrayType.Type;

                if (ourMethod.Name == "Set")
                {
                    instruction = ProcessStoreArrayElement(op, arrayType);
                    return instruction;
                }
                else
                {
                    AnalysisNetBytecode.LoadArrayElementOperation arrayOp = OperationHelper.ToLoadArrayElementOperation(ourMethod.Name);

                    instruction = ProcessLoadArrayElement(op, arrayOp, arrayType);
                    return instruction;
                }
            }

            instruction = new AnalysisNetBytecode.MethodCallInstruction((uint)op.Offset, operation, ourMethod);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessMethodCallIndirect(Cecil.Cil.Instruction op)
        {
            Cecil.FunctionPointerType cciFunctionPointer = op.Operand as Cecil.FunctionPointerType;
            AnalysisNet.Types.FunctionPointerType ourFunctionPointer = typeExtractor.ExtractType(cciFunctionPointer) as AnalysisNet.Types.FunctionPointerType;

            AnalysisNetBytecode.IndirectMethodCallInstruction instruction = new AnalysisNetBytecode.IndirectMethodCallInstruction((uint)op.Offset, ourFunctionPointer);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessSizeof(Cecil.Cil.Instruction op)
        {
            Cecil.TypeReference cciType = op.Operand as Cecil.TypeReference;
            AnalysisNet.Types.IType ourType = typeExtractor.ExtractType(cciType);

            AnalysisNetBytecode.SizeofInstruction instruction = new AnalysisNetBytecode.SizeofInstruction((uint)op.Offset, ourType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessUnaryConditionalBranch(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.BranchOperation operation = OperationHelper.ToBranchOperation(op.OpCode.Code);
            uint target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;

            AnalysisNetBytecode.BranchInstruction instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, operation, target);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessBinaryConditionalBranch(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.BranchOperation operation = OperationHelper.ToBranchOperation(op.OpCode.Code);
            bool unsigned = OperationHelper.OperandsAreUnsigned(op.OpCode.Code);
            uint target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;

            AnalysisNetBytecode.BranchInstruction instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, operation, target)
            {
                UnsignedOperands = unsigned
            };
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLeave(Cecil.Cil.Instruction op)
        {
            uint target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;
            AnalysisNetBytecode.BranchInstruction instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, AnalysisNetBytecode.BranchOperation.Leave, target);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessUnconditionalBranch(Cecil.Cil.Instruction op)
        {
            uint target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;
            AnalysisNetBytecode.BranchInstruction instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, AnalysisNetBytecode.BranchOperation.Branch, target);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadConstant(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.LoadOperation operation = OperationHelper.ToLoadOperation(op.OpCode.Code);
            AnalysisNet.Types.IType type = OperationHelper.GetOperationType(op.OpCode.Code);
            object value = OperationHelper.GetOperationConstant(op);
            AnalysisNetTac.Values.Constant source = new AnalysisNetTac.Values.Constant(value) { Type = type };

            AnalysisNetBytecode.LoadInstruction instruction = new AnalysisNetBytecode.LoadInstruction((uint)op.Offset, operation, source);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadArgument(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.LoadOperation operation = OperationHelper.ToLoadOperation(op.OpCode.Code);
            AnalysisNetTac.Values.IVariable source = thisParameter;

            int argIdx = -1;
            switch (op.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Ldarg_0: argIdx = 0; break;
                case Mono.Cecil.Cil.Code.Ldarg_1: argIdx = 1; break;
                case Mono.Cecil.Cil.Code.Ldarg_2: argIdx = 2; break;
                case Mono.Cecil.Cil.Code.Ldarg_3: argIdx = 3; break;
            }

            if (argIdx > -1)
            {
                if (thisParameter != null && argIdx == 0)
                {
                    source = thisParameter;
                }
                else
                {
                    int hasThis = thisParameter != null ? 1 : 0;
                    source = parameters[argIdx - hasThis];
                }
            }

            if (op.Operand is Cecil.ParameterDefinition)
            {
                Cecil.ParameterDefinition parameter = op.Operand as Cecil.ParameterDefinition;
                source = parameters[parameter.Index];
            }

            if (source == null)
            {
                throw new Exception("source cannot be null.");
            }

            AnalysisNetBytecode.LoadInstruction instruction = new AnalysisNetBytecode.LoadInstruction((uint)op.Offset, operation, source);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadLocal(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.LoadOperation operation = OperationHelper.ToLoadOperation(op.OpCode.Code);

            AnalysisNetTac.Values.IVariable source = null;
            int localIdx = -1;
            switch (op.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Ldloc_0: localIdx = 0; break;
                case Mono.Cecil.Cil.Code.Ldloc_1: localIdx = 1; break;
                case Mono.Cecil.Cil.Code.Ldloc_2: localIdx = 2; break;
                case Mono.Cecil.Cil.Code.Ldloc_3: localIdx = 3; break;
                case Mono.Cecil.Cil.Code.Ldloc_S:
                case Mono.Cecil.Cil.Code.Ldloca_S:
                case Mono.Cecil.Cil.Code.Ldloc:
                case Mono.Cecil.Cil.Code.Ldloca:
                    Cecil.Cil.VariableDefinition varDef = (Cecil.Cil.VariableDefinition)op.Operand;
                    source = locals[varDef.Index]; break;
                default:
                    throw new NotImplementedException();
            }

            if (localIdx > -1)
            {
                source = locals[localIdx];
            }

            AnalysisNetBytecode.LoadInstruction instruction = new AnalysisNetBytecode.LoadInstruction((uint)op.Offset, operation, source);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadIndirect(Cecil.Cil.Instruction op)
        {
            AnalysisNet.Types.IType type = OperationHelper.GetOperationType(op.OpCode.Code);
            if (op.OpCode.Code == Mono.Cecil.Cil.Code.Ldobj)
            {
                type = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            }

            AnalysisNetBytecode.LoadIndirectInstruction instruction = new AnalysisNetBytecode.LoadIndirectInstruction((uint)op.Offset, type);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadField(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.LoadFieldOperation operation = OperationHelper.ToLoadFieldOperation(op.OpCode.Code);
            Cecil.FieldReference cciField = op.Operand as Cecil.FieldReference;
            bool isStatic = op.OpCode.Code == Cecil.Cil.OpCodes.Ldsfld.Code || op.OpCode.Code == Cecil.Cil.OpCodes.Ldsflda.Code;
            AnalysisNet.Types.FieldReference ourField = typeExtractor.ExtractField(cciField, isStatic);

            AnalysisNetBytecode.LoadFieldInstruction instruction = new AnalysisNetBytecode.LoadFieldInstruction((uint)op.Offset, operation, ourField);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadMethodAddress(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.LoadMethodAddressOperation operation = OperationHelper.ToLoadMethodAddressOperation(op.OpCode.Code);
            Cecil.MethodReference cciMethod = op.Operand as Cecil.MethodReference;
            AnalysisNet.Types.IMethodReference ourMethod = typeExtractor.ExtractMethod(cciMethod);

            AnalysisNetBytecode.LoadMethodAddressInstruction instruction = new AnalysisNetBytecode.LoadMethodAddressInstruction((uint)op.Offset, operation, ourMethod);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessLoadToken(Cecil.Cil.Instruction op)
        {
            Cecil.MemberReference cciToken = op.Operand as Cecil.MemberReference;
            AnalysisNet.Types.IMetadataReference ourToken = typeExtractor.ExtractToken(cciToken);

            AnalysisNetBytecode.LoadTokenInstruction instruction = new AnalysisNetBytecode.LoadTokenInstruction((uint)op.Offset, ourToken);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreArgument(Cecil.Cil.Instruction op)
        {
            AnalysisNetTac.Values.IVariable dest = thisParameter;

            if (op.Operand is Cecil.ParameterDefinition parameter)
            {
                dest = parameters[parameter.Index];
            }

            AnalysisNetBytecode.StoreInstruction instruction = new AnalysisNetBytecode.StoreInstruction((uint)op.Offset, dest);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreLocal(Cecil.Cil.Instruction op)
        {
            int localIdx = -1;
            Cecil.Cil.VariableDefinition variable = null;
            switch (op.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Stloc_S:
                case Mono.Cecil.Cil.Code.Stloc: variable = (Cecil.Cil.VariableDefinition)op.Operand; break;
                case Mono.Cecil.Cil.Code.Stloc_0: localIdx = 0; break;
                case Mono.Cecil.Cil.Code.Stloc_1: localIdx = 1; break;
                case Mono.Cecil.Cil.Code.Stloc_2: localIdx = 2; break;
                case Mono.Cecil.Cil.Code.Stloc_3: localIdx = 3; break;
                default:
                    throw new NotImplementedException();
            }

            AnalysisNetTac.Values.IVariable dest;
            if (variable != null)
            {
                dest = locals[variable.Index];
            }
            else
            {
                dest = locals[localIdx];
            }

            AnalysisNetBytecode.StoreInstruction instruction = new AnalysisNetBytecode.StoreInstruction((uint)op.Offset, dest);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreIndirect(Cecil.Cil.Instruction op)
        {
            AnalysisNet.Types.IType type = OperationHelper.GetOperationType(op.OpCode.Code);
            if (op.OpCode.Code == Mono.Cecil.Cil.Code.Stobj)
            {
                type = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            }

            AnalysisNetBytecode.StoreIndirectInstruction instruction = new AnalysisNetBytecode.StoreIndirectInstruction((uint)op.Offset, type);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreField(Cecil.Cil.Instruction op)
        {
            Cecil.FieldReference cciField = op.Operand as Cecil.FieldReference;//op.Operand as Cci.IFieldReference;
            AnalysisNet.Types.FieldReference ourField = typeExtractor.ExtractField(cciField, op.OpCode.Code == Cecil.Cil.Code.Stsfld);

            AnalysisNetBytecode.StoreFieldInstruction instruction = new AnalysisNetBytecode.StoreFieldInstruction((uint)op.Offset, ourField);
            return instruction;
        }
        private AnalysisNet.IInstruction ProcessInitObj(Cecil.Cil.Instruction op)
        {
            AnalysisNet.Types.IType type = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            AnalysisNetBytecode.InitObjInstruction instruction = new AnalysisNetBytecode.InitObjInstruction((uint)op.Offset, type);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessBasic(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.BasicOperation operation = OperationHelper.ToBasicOperation(op.OpCode.Code);
            bool overflow = OperationHelper.PerformsOverflowCheck(op.OpCode.Code);
            bool unsigned = OperationHelper.OperandsAreUnsigned(op.OpCode.Code);

            AnalysisNetBytecode.BasicInstruction instruction = new AnalysisNetBytecode.BasicInstruction((uint)op.Offset, operation)
            {
                OverflowCheck = overflow,
                UnsignedOperands = unsigned
            };
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessConversion(Cecil.Cil.Instruction op)
        {
            AnalysisNetBytecode.ConvertOperation operation = OperationHelper.ToConvertOperation(op.OpCode.Code);
            bool overflow = OperationHelper.PerformsOverflowCheck(op.OpCode.Code);
            bool unsigned = OperationHelper.OperandsAreUnsigned(op.OpCode.Code);

            Cecil.TypeReference cciType = op.Operand as Cecil.TypeReference;
            AnalysisNet.Types.IType ourType = OperationHelper.GetOperationType(op.OpCode.Code);

            if (operation == AnalysisNetBytecode.ConvertOperation.Box)
            {
                ourType = typeExtractor.ExtractType(cciType);
            }
            else if (operation == AnalysisNetBytecode.ConvertOperation.Conv)
            {
                ourType = OperationHelper.GetOperationType(op.OpCode.Code);
            }
            else if (operation == AnalysisNetBytecode.ConvertOperation.Cast)
            {
                ourType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            }

            AnalysisNetBytecode.ConvertInstruction instruction = new AnalysisNetBytecode.ConvertInstruction((uint)op.Offset, operation, ourType)
            {
                OverflowCheck = overflow,
                UnsignedOperands = unsigned
            };
            return instruction;
        }

        private string GetLocalSourceName(Cecil.Cil.VariableDefinition local)
        {
            //var name = local.Name.Value;
            string name = local.ToString();

            //if (sourceLocationProvider != null)
            //{
            //    bool isCompilerGenerated;
            //    name = sourceLocationProvider.GetSourceNameFor(local, out isCompilerGenerated);
            //}

            return name;
        }
    }
}
