﻿using System;
using System.Collections.Generic;
using Bytecode = Model.Bytecode;
using TacInstructions = Model.ThreeAddressCode.Instructions;

namespace tac2cil
{
    public static class Extensions
    {
        public static Bytecode.BasicOperation ToBasicOperation(this TacInstructions.BinaryOperation operation)
        {
            switch (operation)
            {
                case TacInstructions.BinaryOperation.Add: return Bytecode.BasicOperation.Add;
                case TacInstructions.BinaryOperation.And: return Bytecode.BasicOperation.And;
                case TacInstructions.BinaryOperation.Eq: return Bytecode.BasicOperation.Eq;
                case TacInstructions.BinaryOperation.Gt: return Bytecode.BasicOperation.Gt;
                case TacInstructions.BinaryOperation.Lt: return Bytecode.BasicOperation.Lt;
                case TacInstructions.BinaryOperation.Div: return Bytecode.BasicOperation.Div;
                case TacInstructions.BinaryOperation.Mul: return Bytecode.BasicOperation.Mul;
                case TacInstructions.BinaryOperation.Or: return Bytecode.BasicOperation.Or;
                case TacInstructions.BinaryOperation.Rem: return Bytecode.BasicOperation.Rem;
                case TacInstructions.BinaryOperation.Shl: return Bytecode.BasicOperation.Shl;
                case TacInstructions.BinaryOperation.Shr: return Bytecode.BasicOperation.Shr;
                case TacInstructions.BinaryOperation.Sub: return Bytecode.BasicOperation.Sub;
                case TacInstructions.BinaryOperation.Xor: return Bytecode.BasicOperation.Xor;

                default: throw new NotImplementedException();
            }
        }

        public static void AddRange<T>(this IList<T> t, IList<T> x)
        {
            foreach (var elem in x)
                t.Add(elem);
        }
    }
}
