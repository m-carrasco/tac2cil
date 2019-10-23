// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using AnalysisNet = Model;
using AnalysisNetTac = Model.ThreeAddressCode;
using AnalysisNetBytecode = Model.Bytecode;
using Cecil = Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CecilProvider
{
    internal struct FakeArrayType : AnalysisNet.Types.IBasicType
    {
        public AnalysisNet.Types.ArrayType Type { get; private set; }

        public FakeArrayType(AnalysisNet.Types.ArrayType type)
        {
            this.Type = type;
        }

        public AnalysisNet.IAssemblyReference ContainingAssembly
        {
            get { return null; }
        }

        public string ContainingNamespace
        {
            get { return string.Empty; }
        }

        public string Name
        {
            get { return "FakeArray"; }
        }

        public string GenericName
        {
            get { return this.Name; }
        }

        public IList<AnalysisNet.Types.IType> GenericArguments
        {
            get { return null; }
        }

        public AnalysisNet.Types.IBasicType GenericType
        {
            get { return null; }
        }

        public AnalysisNet.Types.TypeDefinition ResolvedType
        {
            get { return null; }
        }

        public AnalysisNet.Types.TypeKind TypeKind
        {
            get { return AnalysisNet.Types.TypeKind.ReferenceType; }
        }

        public ISet<AnalysisNet.Types.CustomAttribute> Attributes
        {
            get { return null; }
        }

        public int GenericParameterCount
        {
            get { return 0; }
        }

        public AnalysisNet.Types.IBasicType ContainingType
        {
            get { return null; }
        }
    }

    internal class CodeProvider
	{
		private TypeExtractor typeExtractor;
		private IDictionary<Cecil.ParameterDefinition, AnalysisNetTac.Values.IVariable> parameters;
		private IDictionary<Cecil.Cil.VariableDefinition, AnalysisNetTac.Values.IVariable> locals;
		private AnalysisNetTac.Values.IVariable thisParameter;

        private Cecil.Cil.MethodBody cecilBody;
		public CodeProvider(TypeExtractor typeExtractor)
		{
			this.typeExtractor = typeExtractor;
			this.parameters = new Dictionary<Cecil.ParameterDefinition, AnalysisNetTac.Values.IVariable>();
			this.locals = new Dictionary<Cecil.Cil.VariableDefinition, AnalysisNetTac.Values.IVariable>();
		}
		
		public AnalysisNet.Types.MethodBody ExtractBody(Cecil.Cil.MethodBody cciBody)
		{
            cecilBody = cciBody;
			var ourBody = new AnalysisNet.Types.MethodBody(AnalysisNet.Types.MethodBodyKind.Bytecode);

			ourBody.MaxStack = (ushort)cciBody.MaxStackSize;
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
				//var isByReference = methoddef.DeclaringType.IsValueType;
                var type = typeExtractor.ExtractType(methoddef.DeclaringType);//typeExtractor.ExtractType(methoddef.DeclaringType, isByReference);
                var v = new AnalysisNetTac.Values.LocalVariable("this", true) { Type = type };

				ourParameters.Add(v);
				thisParameter = v;
			}

			foreach (var parameter in methoddef.Parameters)
			{
                //var type = typeExtractor.ExtractType(parameter.ParameterType, parameter.IsByReference);
                var type = typeExtractor.ExtractType(parameter.ParameterType);
                var v = new AnalysisNetTac.Values.LocalVariable(parameter.Name, true) { Type = type };

				ourParameters.Add(v);
				parameters.Add(parameter, v);
			}
		}

		private void ExtractLocalVariables(IEnumerable<Cecil.Cil.VariableDefinition> cciLocalVariables, IList<AnalysisNetTac.Values.IVariable> ourLocalVariables)
		{
			foreach (var local in cciLocalVariables)
			{
				var name = GetLocalSourceName(local);
                //var type = typeExtractor.ExtractType(local.VariableType, local);
                var type = typeExtractor.ExtractType(local.VariableType);
                var v = new AnalysisNetTac.Values.LocalVariable(name) { Type = type };

				ourLocalVariables.Add(v);
				locals.Add(local, v);
			}
		}

		private void ExtractExceptionInformation(IEnumerable<Cecil.Cil.ExceptionHandler> cciExceptionInformation, IList<AnalysisNet.ProtectedBlock> ourExceptionInformation)
		{
			foreach (var cciExceptionInfo in cciExceptionInformation)
			{
				var tryHandler = new AnalysisNet.ProtectedBlock((uint)cciExceptionInfo.TryStart.Offset, (uint)cciExceptionInfo.TryEnd.Offset);

				switch (cciExceptionInfo.HandlerType)
				{
					case Cecil.Cil.ExceptionHandlerType.Filter:
						var filterExceptionType = typeExtractor.ExtractType(cciExceptionInfo.CatchType);
						var filterHandler = new AnalysisNet.FilterExceptionHandler((uint)cciExceptionInfo.FilterStart.Offset, (uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset, filterExceptionType);
						tryHandler.Handler = filterHandler;
						break;

					case Cecil.Cil.ExceptionHandlerType.Catch:
						var catchExceptionType = typeExtractor.ExtractType(cciExceptionInfo.CatchType);
						var catchHandler = new AnalysisNet.CatchExceptionHandler((uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset, catchExceptionType);
						tryHandler.Handler = catchHandler;
						break;

					case Cecil.Cil.ExceptionHandlerType.Fault:
						var faultHandler = new AnalysisNet.FaultExceptionHandler((uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset);
						tryHandler.Handler = faultHandler;
						break;

					case Cecil.Cil.ExceptionHandlerType.Finally:
						var finallyHandler = new AnalysisNet.FinallyExceptionHandler((uint)cciExceptionInfo.HandlerStart.Offset, (uint)cciExceptionInfo.HandlerEnd.Offset);
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
			foreach (var op in operations)
			{
				var instruction = ExtractInstruction(op);
				instructions.Add(instruction);
			}
		}

		private AnalysisNet.IInstruction ExtractInstruction(Cecil.Cil.Instruction operation)
		{
            AnalysisNet.IInstruction instruction = null;

            // cecil does not have an enum we require it for the switch statement
            var code = operation.OpCode.Code;
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
				//	// If code out there actually uses this, I need to know sooner rather than later.
				//	// TODO: need object model support
				//	throw new NotImplementedException("Invalid opcode: No.");

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
            //if (arrayType == null)
            //{
            //    AnalysisNet.Types.IType elementType =  OperationHelper.GetOperationType(op.OpCode.Code);
            //    arrayType = new AnalysisNet.Types.ArrayType(elementType);
            //}
            var instruction = new AnalysisNetBytecode.LoadArrayElementInstruction((uint)op.Offset, operation, arrayType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreArrayElement(Cecil.Cil.Instruction op, AnalysisNet.Types.ArrayType arrayType)
        {
            //if (arrayType == null)
            //    arrayType = new AnalysisNet.Types.ArrayType(OperationHelper.GetOperationType(op.Opcode));

            var instruction = new AnalysisNetBytecode.StoreArrayElementInstruction((uint)op.Offset, arrayType);
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
                    var extractedType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
                    arrayType = new AnalysisNet.Types.ArrayType(extractedType);
                    break;
                default:
                    arrayType = new AnalysisNet.Types.ArrayType(OperationHelper.GetOperationType(op.OpCode.Code));
                    break;
            }

            if (arrayType == null)
                throw new NotImplementedException();

            var instruction = new AnalysisNetBytecode.StoreArrayElementInstruction((uint)op.Offset, arrayType);
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
                throw new NotImplementedException();

            var instruction = new AnalysisNetBytecode.LoadArrayElementInstruction((uint)op.Offset, operation, arrayType);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessConstrained(Cecil.Cil.Instruction op)
        {
            var thisType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            var ins = new AnalysisNetBytecode.ConstrainedInstruction((uint)op.Offset, thisType);
            return ins;
        }

        private AnalysisNet.IInstruction ProcessSwitch(Cecil.Cil.Instruction op)
		{
			var targets = op.Operand as Cecil.Cil.Instruction[];

			var instruction = new AnalysisNetBytecode.SwitchInstruction((uint)op.Offset, targets.Select(t => (uint)t.Offset));
			return instruction;
		}
		private AnalysisNet.IInstruction ProcessCreateArray(Cecil.Cil.Instruction op)
		{
			var cciArrayType = Cecil.Rocks.TypeReferenceRocks.MakeArrayType(op.Operand as Cecil.TypeReference);
			var ourArrayType = typeExtractor.ExtractType(cciArrayType) as AnalysisNet.Types.ArrayType;

            return CreateArray((uint)op.Offset, ourArrayType);
            //OperationHelper.CreateArrayWithLowerBounds(op.OpCode.Code);
            //var instruction = new AnalysisNetBytecode.CreateArrayInstruction((uint)op.Offset, ourArrayType);
            //instruction.WithLowerBound = withLowerBound;
            //return instruction;
        }
        private AnalysisNet.IInstruction CreateArray(uint offset, AnalysisNet.Types.ArrayType arrayType, bool withLowerBound = false)
        {
            var instruction = new AnalysisNetBytecode.CreateArrayInstruction(offset, arrayType);
            instruction.WithLowerBound = withLowerBound;
            return instruction;
        }
        private AnalysisNet.IInstruction ProcessCreateObject(Cecil.Cil.Instruction op)
		{
			var cciMethod = op.Operand as Cecil.MethodReference;
			var ourMethod = typeExtractor.ExtractMethod(cciMethod);

            if (ourMethod.ContainingType is FakeArrayType fakeArrayType)
            {
                var withLowerBounds = ourMethod.Parameters.Count > fakeArrayType.Type.Rank;
                return CreateArray((uint)op.Offset, fakeArrayType.Type, withLowerBounds);
            }

			var instruction = new AnalysisNetBytecode.CreateObjectInstruction((uint)op.Offset, ourMethod);
			return instruction;
		}
		private AnalysisNet.IInstruction ProcessMethodCall(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToMethodCallOperation(op.OpCode.Code);
			var cciMethod = op.Operand as Cecil.MethodReference;
			var ourMethod = typeExtractor.ExtractMethod(cciMethod);

            AnalysisNet.IInstruction instruction;

            if (ourMethod.ContainingType is FakeArrayType fakeArrayType)
            {
                var arrayType = fakeArrayType.Type;

                if (ourMethod.Name == "Set")
                {
                    instruction = ProcessStoreArrayElement(op, arrayType);
                    return instruction;
                }
                else
                {
                    var arrayOp = OperationHelper.ToLoadArrayElementOperation(ourMethod.Name);

                    instruction = ProcessLoadArrayElement(op, arrayOp, arrayType);
                    return instruction;
                }
            }

            instruction = new AnalysisNetBytecode.MethodCallInstruction((uint)op.Offset, operation, ourMethod);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessMethodCallIndirect(Cecil.Cil.Instruction op)
		{
            var cciFunctionPointer = op.Operand as Cecil.FunctionPointerType;//Cci.IFunctionPointerTypeReference;
			var ourFunctionPointer = typeExtractor.ExtractType(cciFunctionPointer) as AnalysisNet.Types.FunctionPointerType;

			var instruction = new AnalysisNetBytecode.IndirectMethodCallInstruction((uint)op.Offset, ourFunctionPointer);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessSizeof(Cecil.Cil.Instruction op)
		{
			var cciType = op.Operand as Cecil.TypeReference;
			var ourType = typeExtractor.ExtractType(cciType);

			var instruction = new AnalysisNetBytecode.SizeofInstruction((uint)op.Offset, ourType);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessUnaryConditionalBranch(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToBranchOperation(op.OpCode.Code);
			var target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;

            var instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, operation, target);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessBinaryConditionalBranch(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToBranchOperation(op.OpCode.Code);
			var unsigned = OperationHelper.OperandsAreUnsigned(op.OpCode.Code);
			var target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;

			var instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, operation, target);
			instruction.UnsignedOperands = unsigned;
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLeave(Cecil.Cil.Instruction op)
		{
            var target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;
            var instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, AnalysisNetBytecode.BranchOperation.Leave, target);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessUnconditionalBranch(Cecil.Cil.Instruction op)
		{
            var target = (uint)((Cecil.Cil.Instruction)op.Operand).Offset;
            var instruction = new AnalysisNetBytecode.BranchInstruction((uint)op.Offset, AnalysisNetBytecode.BranchOperation.Branch, target);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadConstant(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToLoadOperation(op.OpCode.Code);
			var type = OperationHelper.GetOperationType(op.OpCode.Code);
			var value = OperationHelper.GetOperationConstant(op);
			var source = new AnalysisNetTac.Values.Constant(value) { Type = type };

			var instruction = new AnalysisNetBytecode.LoadInstruction((uint)op.Offset, operation, source);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadArgument(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToLoadOperation(op.OpCode.Code);
			var source = thisParameter;

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
                    source = thisParameter;
                else
                {
                    var hasThis = thisParameter != null ? 1 : 0;
                    // inefficient
                    source = parameters.Where(kv => kv.Key.Index == argIdx - hasThis).First().Value;
                }

            }
            
            //if (op.Operand is Cci.IParameterDefinition)
            if (op.Operand is Cecil.ParameterDefinition)
            {
                //var parameter = op.Operand as Cci.IParameterDefinition;
                var parameter = op.Operand as Cecil.ParameterDefinition;
                source = parameters[parameter];
			}

            if (source == null)
                throw new Exception("source cannot be null.");

			var instruction = new AnalysisNetBytecode.LoadInstruction((uint)op.Offset, operation, source);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadLocal(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToLoadOperation(op.OpCode.Code);

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
                case Mono.Cecil.Cil.Code.Ldloca: source = locals[(Cecil.Cil.VariableDefinition)op.Operand]; break;
                default:
                    throw new NotImplementedException();
            }

            if (localIdx > -1)
                source = locals.Where(kv => kv.Key.Index == localIdx).First().Value;
			//var local = op.Operand as Cecil.Cil.VariableDefinition;
			//var source = locals[local];

			var instruction = new AnalysisNetBytecode.LoadInstruction((uint)op.Offset, operation, source);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadIndirect(Cecil.Cil.Instruction op)
		{
            var type = OperationHelper.GetOperationType(op.OpCode.Code);
            if (op.OpCode.Code == Mono.Cecil.Cil.Code.Ldobj)
                type = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            var instruction = new AnalysisNetBytecode.LoadIndirectInstruction((uint)op.Offset, type);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadField(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToLoadFieldOperation(op.OpCode.Code);
			var cciField = op.Operand as Cecil.FieldReference;
            bool isStatic = op.OpCode.Code == Cecil.Cil.OpCodes.Ldsfld.Code || op.OpCode.Code == Cecil.Cil.OpCodes.Ldsflda.Code;
            var ourField = typeExtractor.ExtractField(cciField, isStatic);

			var instruction = new AnalysisNetBytecode.LoadFieldInstruction((uint)op.Offset, operation, ourField);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadMethodAddress(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToLoadMethodAddressOperation(op.OpCode.Code);
			var cciMethod = op.Operand as Cecil.MethodReference;
			var ourMethod = typeExtractor.ExtractMethod(cciMethod);

			var instruction = new AnalysisNetBytecode.LoadMethodAddressInstruction((uint)op.Offset, operation, ourMethod);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessLoadToken(Cecil.Cil.Instruction op)
		{
			var cciToken = op.Operand as Cecil.MemberReference;
			var ourToken = typeExtractor.ExtractToken(cciToken);

			var instruction = new AnalysisNetBytecode.LoadTokenInstruction((uint)op.Offset, ourToken);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessStoreArgument(Cecil.Cil.Instruction op)
		{
			var dest = thisParameter;

            //if (op.Operand is Cci.IParameterDefinition)
            if (op.Operand is Cecil.ParameterDefinition parameter)
            {
				//var parameter = op.Operand as Cci.IParameterDefinition;
				dest = parameters[parameter];
			}

			var instruction = new AnalysisNetBytecode.StoreInstruction((uint)op.Offset, dest);
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessStoreLocal(Cecil.Cil.Instruction op)
        {
            int localIdx = -1;
            Cecil.Cil.VariableDefinition variable = null;
            switch (op.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Stloc_S:
                case Mono.Cecil.Cil.Code.Stloc: variable = (Cecil.Cil.VariableDefinition)op.Operand;  break;
                case Mono.Cecil.Cil.Code.Stloc_0: localIdx = 0; break; 
                case Mono.Cecil.Cil.Code.Stloc_1: localIdx = 1; break;
                case Mono.Cecil.Cil.Code.Stloc_2: localIdx = 2; break;
                case Mono.Cecil.Cil.Code.Stloc_3: localIdx = 3; break;
                default:
                    throw new NotImplementedException();
            }

            AnalysisNetTac.Values.IVariable dest;
            if (variable != null)
                dest = locals[variable];
            else
                dest = locals.Where(kv => kv.Key.Index == localIdx).First().Value;

			//var local = op.Operand as Cecil.Cil.VariableDefinition;
			//var dest = locals[local];

			var instruction = new AnalysisNetBytecode.StoreInstruction((uint)op.Offset, dest);
			return instruction;
		}

        private AnalysisNet.IInstruction ProcessStoreIndirect(Cecil.Cil.Instruction op)
        {
            var type = OperationHelper.GetOperationType(op.OpCode.Code);
            if (op.OpCode.Code == Mono.Cecil.Cil.Code.Stobj)
                type = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            var instruction = new AnalysisNetBytecode.StoreIndirectInstruction((uint)op.Offset, type);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessStoreField(Cecil.Cil.Instruction op)
		{
			var cciField = op.Operand as Cecil.FieldReference;//op.Operand as Cci.IFieldReference;
            var ourField = typeExtractor.ExtractField(cciField, op.OpCode.Code == Cecil.Cil.Code.Stsfld);

			var instruction = new AnalysisNetBytecode.StoreFieldInstruction((uint)op.Offset, ourField);
			return instruction;
		}
        private AnalysisNet.IInstruction ProcessInitObj(Cecil.Cil.Instruction op)
        {
            var type = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            var instruction = new AnalysisNetBytecode.InitObjInstruction((uint)op.Offset, type);
            return instruction;
        }

        private AnalysisNet.IInstruction ProcessBasic(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToBasicOperation(op.OpCode.Code);
			var overflow = OperationHelper.PerformsOverflowCheck(op.OpCode.Code);
			var unsigned = OperationHelper.OperandsAreUnsigned(op.OpCode.Code);

			var instruction = new AnalysisNetBytecode.BasicInstruction((uint)op.Offset, operation);
			instruction.OverflowCheck = overflow;
			instruction.UnsignedOperands = unsigned;
			return instruction;
		}

		private AnalysisNet.IInstruction ProcessConversion(Cecil.Cil.Instruction op)
		{
			var operation = OperationHelper.ToConvertOperation(op.OpCode.Code);
			var overflow = OperationHelper.PerformsOverflowCheck(op.OpCode.Code);
			var unsigned = OperationHelper.OperandsAreUnsigned(op.OpCode.Code);

			var cciType = op.Operand as Cecil.TypeReference;
            var ourType = OperationHelper.GetOperationType(op.OpCode.Code);

			if (operation == AnalysisNetBytecode.ConvertOperation.Box)
			{
                if (cciType.IsValueType)
                    ourType = AnalysisNet.Types.PlatformTypes.Object;
                else
                    ourType = typeExtractor.ExtractType(cciType);
			}
			else if (operation == AnalysisNetBytecode.ConvertOperation.Conv)
			{
				ourType = OperationHelper.GetOperationType(op.OpCode.Code);
			} else if (operation == AnalysisNetBytecode.ConvertOperation.Cast)
            {
                ourType = typeExtractor.ExtractType(op.Operand as Cecil.TypeReference);
            }
			
			var instruction = new AnalysisNetBytecode.ConvertInstruction((uint)op.Offset, operation, ourType);
			instruction.OverflowCheck = overflow;
			instruction.UnsignedOperands = unsigned;
			return instruction;
		}

		private string GetLocalSourceName(Cecil.Cil.VariableDefinition local)
		{
            //var name = local.Name.Value;
            var name = local.ToString();

            //if (sourceLocationProvider != null)
            //{
            //	bool isCompilerGenerated;
            //	name = sourceLocationProvider.GetSourceNameFor(local, out isCompilerGenerated);
            //}

            return name;
		}
	}
}
