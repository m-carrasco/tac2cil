// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model.Bytecode;
using Model.Types;
using Model;

namespace CecilProvider
{
	public static class OperationHelper
	{
		public static BasicOperation ToBasicOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Neg:			return BasicOperation.Neg;
				case Mono.Cecil.Cil.Code.Not:			return BasicOperation.Not;
				case Mono.Cecil.Cil.Code.Add:
				case Mono.Cecil.Cil.Code.Add_Ovf:
				case Mono.Cecil.Cil.Code.Add_Ovf_Un:	return BasicOperation.Add;
				case Mono.Cecil.Cil.Code.And:			return BasicOperation.And;
				case Mono.Cecil.Cil.Code.Ceq:			return BasicOperation.Eq;
				case Mono.Cecil.Cil.Code.Cgt:
				case Mono.Cecil.Cil.Code.Cgt_Un:		return BasicOperation.Gt;
				case Mono.Cecil.Cil.Code.Clt:
				case Mono.Cecil.Cil.Code.Clt_Un:		return BasicOperation.Lt;
				case Mono.Cecil.Cil.Code.Div:
				case Mono.Cecil.Cil.Code.Div_Un:		return BasicOperation.Div;
				case Mono.Cecil.Cil.Code.Mul:
				case Mono.Cecil.Cil.Code.Mul_Ovf:
				case Mono.Cecil.Cil.Code.Mul_Ovf_Un:	return BasicOperation.Mul;
				case Mono.Cecil.Cil.Code.Or:			return BasicOperation.Or;
				case Mono.Cecil.Cil.Code.Rem:
				case Mono.Cecil.Cil.Code.Rem_Un:		return BasicOperation.Rem;
				case Mono.Cecil.Cil.Code.Shl:			return BasicOperation.Shl;
				case Mono.Cecil.Cil.Code.Shr:
				case Mono.Cecil.Cil.Code.Shr_Un:		return BasicOperation.Shr;
				case Mono.Cecil.Cil.Code.Sub:
				case Mono.Cecil.Cil.Code.Sub_Ovf:
				case Mono.Cecil.Cil.Code.Sub_Ovf_Un:	return BasicOperation.Sub;
				case Mono.Cecil.Cil.Code.Xor:			return BasicOperation.Xor;
				case Mono.Cecil.Cil.Code.Endfilter:	return BasicOperation.EndFilter;
				case Mono.Cecil.Cil.Code.Endfinally:	return BasicOperation.EndFinally;
				case Mono.Cecil.Cil.Code.Throw:		return BasicOperation.Throw;
				case Mono.Cecil.Cil.Code.Rethrow:		return BasicOperation.Rethrow;
				case Mono.Cecil.Cil.Code.Nop:			return BasicOperation.Nop;
				case Mono.Cecil.Cil.Code.Pop:			return BasicOperation.Pop;
				case Mono.Cecil.Cil.Code.Dup:			return BasicOperation.Dup;
				case Mono.Cecil.Cil.Code.Localloc:	return BasicOperation.LocalAllocation;
				case Mono.Cecil.Cil.Code.Initblk:		return BasicOperation.InitBlock;
				//case Mono.Cecil.Cil.Code.Initobj:		return BasicOperation.InitObject;
				case Mono.Cecil.Cil.Code.Cpblk:		return BasicOperation.CopyBlock;
				case Mono.Cecil.Cil.Code.Cpobj:		return BasicOperation.CopyObject;
				case Mono.Cecil.Cil.Code.Ret:			return BasicOperation.Return;
				case Mono.Cecil.Cil.Code.Ldlen:		return BasicOperation.LoadArrayLength;
				case Mono.Cecil.Cil.Code.Break:		return BasicOperation.Breakpoint;
				
				default: throw opcode.ToUnknownValueException();
			}
		}

		public static ConvertOperation ToConvertOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Castclass:
				case Mono.Cecil.Cil.Code.Isinst:		return ConvertOperation.Cast;
				case Mono.Cecil.Cil.Code.Box:			return ConvertOperation.Box;
				case Mono.Cecil.Cil.Code.Unbox:		return ConvertOperation.UnboxPtr;
				case Mono.Cecil.Cil.Code.Unbox_Any:	return ConvertOperation.Unbox;
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
				case Mono.Cecil.Cil.Code.Conv_R_Un:	return ConvertOperation.Conv;

				default: throw opcode.ToUnknownValueException();
			}
		}

		public static BranchOperation ToBranchOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Brfalse:
				case Mono.Cecil.Cil.Code.Brfalse_S:	return BranchOperation.False;
				case Mono.Cecil.Cil.Code.Brtrue:
				case Mono.Cecil.Cil.Code.Brtrue_S:	return BranchOperation.True;
				case Mono.Cecil.Cil.Code.Beq:
				case Mono.Cecil.Cil.Code.Beq_S:		return BranchOperation.Eq;
				case Mono.Cecil.Cil.Code.Bne_Un:
				case Mono.Cecil.Cil.Code.Bne_Un_S:	return BranchOperation.Neq;
				case Mono.Cecil.Cil.Code.Bge:
				case Mono.Cecil.Cil.Code.Bge_S:
				case Mono.Cecil.Cil.Code.Bge_Un:
				case Mono.Cecil.Cil.Code.Bge_Un_S:	return BranchOperation.Ge;
				case Mono.Cecil.Cil.Code.Bgt:
				case Mono.Cecil.Cil.Code.Bgt_S:
				case Mono.Cecil.Cil.Code.Bgt_Un:
				case Mono.Cecil.Cil.Code.Bgt_Un_S:	return BranchOperation.Gt;
				case Mono.Cecil.Cil.Code.Ble:
				case Mono.Cecil.Cil.Code.Ble_S:
				case Mono.Cecil.Cil.Code.Ble_Un:
				case Mono.Cecil.Cil.Code.Ble_Un_S:	return BranchOperation.Le;
				case Mono.Cecil.Cil.Code.Blt:
				case Mono.Cecil.Cil.Code.Blt_S:
				case Mono.Cecil.Cil.Code.Blt_Un:
				case Mono.Cecil.Cil.Code.Blt_Un_S:	return BranchOperation.Lt;
				case Mono.Cecil.Cil.Code.Leave:
				case Mono.Cecil.Cil.Code.Leave_S:		return BranchOperation.Leave;

				default: throw opcode.ToUnknownValueException();
			}
		}

		public static MethodCallOperation ToMethodCallOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Call:		return MethodCallOperation.Static;
				case Mono.Cecil.Cil.Code.Callvirt:	return MethodCallOperation.Virtual;
				case Mono.Cecil.Cil.Code.Jmp:			return MethodCallOperation.Jump;

				default: throw opcode.ToUnknownValueException();
			}
		}

		public static LoadOperation ToLoadOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
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
				case Mono.Cecil.Cil.Code.Ldstr: return LoadOperation.Value;
				case Mono.Cecil.Cil.Code.Ldarg:
				case Mono.Cecil.Cil.Code.Ldarg_0:
				case Mono.Cecil.Cil.Code.Ldarg_1:
				case Mono.Cecil.Cil.Code.Ldarg_2:
				case Mono.Cecil.Cil.Code.Ldarg_3:
				case Mono.Cecil.Cil.Code.Ldarg_S:
				case Mono.Cecil.Cil.Code.Ldloc:
				case Mono.Cecil.Cil.Code.Ldloc_0:
				case Mono.Cecil.Cil.Code.Ldloc_1:
				case Mono.Cecil.Cil.Code.Ldloc_2:
				case Mono.Cecil.Cil.Code.Ldloc_3:
				case Mono.Cecil.Cil.Code.Ldloc_S: return LoadOperation.Content;
				case Mono.Cecil.Cil.Code.Ldarga:
				case Mono.Cecil.Cil.Code.Ldarga_S:
				case Mono.Cecil.Cil.Code.Ldloca:
				case Mono.Cecil.Cil.Code.Ldloca_S: return LoadOperation.Address;

				default: throw opcode.ToUnknownValueException();
			}
		}

		public static LoadFieldOperation ToLoadFieldOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Ldfld:
				case Mono.Cecil.Cil.Code.Ldsfld: return LoadFieldOperation.Content;
				case Mono.Cecil.Cil.Code.Ldflda:
				case Mono.Cecil.Cil.Code.Ldsflda: return LoadFieldOperation.Address;

				default: throw opcode.ToUnknownValueException();
			}
		}

        public static LoadArrayElementOperation ToLoadArrayElementOperation(string methodName)
        {
            LoadArrayElementOperation operation;

            if (methodName == "Get")
            {
                operation = LoadArrayElementOperation.Content;
            }
            else if (methodName == "Address")
            {
                operation = LoadArrayElementOperation.Address;
            }
            else
            {
                var msg = string.Format("Unknown array operation '{0}'", methodName);
                throw new Exception(msg);
            }

            return operation;
        }

        public static LoadMethodAddressOperation ToLoadMethodAddressOperation(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Ldftn: return LoadMethodAddressOperation.Static;
				case Mono.Cecil.Cil.Code.Ldvirtftn: return LoadMethodAddressOperation.Virtual;

				default: throw opcode.ToUnknownValueException();
			}
		}

		public static object GetOperationConstant(Mono.Cecil.Cil.Instruction op)
		{
			switch (op.OpCode.Code)
			{
				case Mono.Cecil.Cil.Code.Ldc_I4_0: return 0;
				case Mono.Cecil.Cil.Code.Ldc_I4_1: return 1;
				case Mono.Cecil.Cil.Code.Ldc_I4_2: return 2;
				case Mono.Cecil.Cil.Code.Ldc_I4_3: return 3;
				case Mono.Cecil.Cil.Code.Ldc_I4_4: return 4;
				case Mono.Cecil.Cil.Code.Ldc_I4_5: return 5;
				case Mono.Cecil.Cil.Code.Ldc_I4_6: return 6;
				case Mono.Cecil.Cil.Code.Ldc_I4_7: return 7;
				case Mono.Cecil.Cil.Code.Ldc_I4_8: return 8;
				case Mono.Cecil.Cil.Code.Ldc_I4_M1: return -1;
				case Mono.Cecil.Cil.Code.Ldc_I4:
				case Mono.Cecil.Cil.Code.Ldc_I4_S:
				case Mono.Cecil.Cil.Code.Ldc_I8:
				case Mono.Cecil.Cil.Code.Ldc_R4:
				case Mono.Cecil.Cil.Code.Ldc_R8:
				case Mono.Cecil.Cil.Code.Ldnull:
				case Mono.Cecil.Cil.Code.Ldstr: return op.Operand;

                //default: throw op.OperationCode.ToUnknownValueException();
                default: throw new NotImplementedException();
            }
		}

		public static IType GetOperationType(Mono.Cecil.Cil.Code opcode)
        {
			switch (opcode)
			{
                case Mono.Cecil.Cil.Code.Ldelem_I:
                case Mono.Cecil.Cil.Code.Ldind_I:
				case Mono.Cecil.Cil.Code.Stind_I:
				case Mono.Cecil.Cil.Code.Stelem_I:
				case Mono.Cecil.Cil.Code.Conv_I:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I_Un:	return PlatformTypes.IntPtr;
                case Mono.Cecil.Cil.Code.Ldelem_I1:
                case Mono.Cecil.Cil.Code.Ldind_I1:
				case Mono.Cecil.Cil.Code.Stind_I1:
				case Mono.Cecil.Cil.Code.Stelem_I1:
				case Mono.Cecil.Cil.Code.Conv_I1:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I1:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I1_Un:	return PlatformTypes.Int8;
                case Mono.Cecil.Cil.Code.Ldelem_I2:
                case Mono.Cecil.Cil.Code.Ldind_I2:
				case Mono.Cecil.Cil.Code.Stind_I2:
				case Mono.Cecil.Cil.Code.Stelem_I2:
				case Mono.Cecil.Cil.Code.Conv_I2:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I2:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I2_Un:	return PlatformTypes.Int16;
                case Mono.Cecil.Cil.Code.Ldelem_I4:
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
				case Mono.Cecil.Cil.Code.Ldind_I4:
				case Mono.Cecil.Cil.Code.Stind_I4:
				case Mono.Cecil.Cil.Code.Stelem_I4:
				case Mono.Cecil.Cil.Code.Conv_I4:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I4:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I4_Un:	return PlatformTypes.Int32;
                case Mono.Cecil.Cil.Code.Ldelem_I8:
                case Mono.Cecil.Cil.Code.Ldc_I8:
				case Mono.Cecil.Cil.Code.Ldind_I8:
				case Mono.Cecil.Cil.Code.Stind_I8:
				case Mono.Cecil.Cil.Code.Stelem_I8:
				case Mono.Cecil.Cil.Code.Conv_I8:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I8:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I8_Un:	return PlatformTypes.Int64;
				case Mono.Cecil.Cil.Code.Conv_U:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U_Un:
				case Mono.Cecil.Cil.Code.Ldlen:			return PlatformTypes.UIntPtr;
                case Mono.Cecil.Cil.Code.Ldelem_U1:
                case Mono.Cecil.Cil.Code.Ldind_U1:
				case Mono.Cecil.Cil.Code.Conv_U1:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U1:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U1_Un:	return PlatformTypes.UInt8;
                case Mono.Cecil.Cil.Code.Ldelem_U2:
                case Mono.Cecil.Cil.Code.Ldind_U2:
				case Mono.Cecil.Cil.Code.Conv_U2:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U2:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U2_Un:	return PlatformTypes.UInt16;
                case Mono.Cecil.Cil.Code.Ldelem_U4:
                case Mono.Cecil.Cil.Code.Ldind_U4:
				case Mono.Cecil.Cil.Code.Conv_U4:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U4:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U4_Un:
				case Mono.Cecil.Cil.Code.Sizeof:			return PlatformTypes.UInt32;
				case Mono.Cecil.Cil.Code.Conv_U8:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U8:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U8_Un:	return PlatformTypes.UInt64;
                case Mono.Cecil.Cil.Code.Ldelem_R4:
                case Mono.Cecil.Cil.Code.Ldc_R4:
				case Mono.Cecil.Cil.Code.Ldind_R4:
				case Mono.Cecil.Cil.Code.Stind_R4:
				case Mono.Cecil.Cil.Code.Stelem_R4:
				case Mono.Cecil.Cil.Code.Conv_R4:			return PlatformTypes.Float32;
                case Mono.Cecil.Cil.Code.Ldelem_R8:
                case Mono.Cecil.Cil.Code.Ldc_R8:
				case Mono.Cecil.Cil.Code.Ldind_R8:
				case Mono.Cecil.Cil.Code.Stind_R8:
				case Mono.Cecil.Cil.Code.Stelem_R8:
				case Mono.Cecil.Cil.Code.Conv_R8:
				case Mono.Cecil.Cil.Code.Conv_R_Un:		return PlatformTypes.Float64;
                case Mono.Cecil.Cil.Code.Ldelem_Ref:
                case Mono.Cecil.Cil.Code.Stelem_Ref:
                case Mono.Cecil.Cil.Code.Stind_Ref:
                case Mono.Cecil.Cil.Code.Ldind_Ref:
                case Mono.Cecil.Cil.Code.Ldnull:			return PlatformTypes.Object;
				case Mono.Cecil.Cil.Code.Ldstr:			return PlatformTypes.String;

				default: return null;
			}
		}

		public static bool OperandsAreUnsigned(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Add_Ovf_Un:
				case Mono.Cecil.Cil.Code.Bge_Un:
				case Mono.Cecil.Cil.Code.Bge_Un_S:
				case Mono.Cecil.Cil.Code.Bgt_Un:
				case Mono.Cecil.Cil.Code.Bgt_Un_S:
				case Mono.Cecil.Cil.Code.Ble_Un:
				case Mono.Cecil.Cil.Code.Ble_Un_S:
				case Mono.Cecil.Cil.Code.Blt_Un:
				case Mono.Cecil.Cil.Code.Blt_Un_S:
				case Mono.Cecil.Cil.Code.Bne_Un:
				case Mono.Cecil.Cil.Code.Bne_Un_S:
				case Mono.Cecil.Cil.Code.Cgt_Un:
				case Mono.Cecil.Cil.Code.Clt_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I1_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I2_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I4_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I8_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U1_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U2_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U4_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U8_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U_Un:
				case Mono.Cecil.Cil.Code.Conv_R_Un:
				case Mono.Cecil.Cil.Code.Div_Un:
				case Mono.Cecil.Cil.Code.Mul_Ovf_Un:
				case Mono.Cecil.Cil.Code.Rem_Un:
				case Mono.Cecil.Cil.Code.Shr_Un:
				case Mono.Cecil.Cil.Code.Sub_Ovf_Un:	return true;

				default:							return false;
			}
		}

		public static bool PerformsOverflowCheck(Mono.Cecil.Cil.Code opcode)
		{
			switch (opcode)
			{
				case Mono.Cecil.Cil.Code.Add_Ovf:
				case Mono.Cecil.Cil.Code.Add_Ovf_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I1:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I1_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I2:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I2_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I4:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I4_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I8:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I8_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_I_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U1:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U1_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U2:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U2_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U4:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U4_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U8:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U8_Un:
				case Mono.Cecil.Cil.Code.Conv_Ovf_U_Un:
				case Mono.Cecil.Cil.Code.Mul_Ovf:
				case Mono.Cecil.Cil.Code.Mul_Ovf_Un:
				case Mono.Cecil.Cil.Code.Sub_Ovf:
				case Mono.Cecil.Cil.Code.Sub_Ovf_Un:	return true;

				default:							return false;
			}
		}

		public static bool CreateArrayWithLowerBounds(Mono.Cecil.Cil.Code opcode)
		{
            throw new NotImplementedException();
			//var result = opcode == Mono.Cecil.Cil.Code.Array_Create_WithLowerBound;
			//return result;
		}
	}
}
