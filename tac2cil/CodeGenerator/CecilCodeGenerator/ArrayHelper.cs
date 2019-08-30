using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    class ArrayHelper
    {
        public static MethodReference ArrayCtor(ArrayType arrayType)
        {
            MethodReference arrayCtor = new MethodReference(".ctor", arrayType.Module.TypeSystem.Void, arrayType);
            arrayCtor.HasThis = true;
            for (int i = 0; i < arrayType.Rank; i++)
                arrayCtor.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));

            return arrayCtor;
        }

        public static MethodReference ArraySet(ArrayType arrayType)
        {
            MethodReference arrayGet = new MethodReference("Set", arrayType.Module.TypeSystem.Void, arrayType);
            arrayGet.HasThis = true;
            arrayGet.Parameters.Add(new ParameterDefinition(arrayType.ElementType));
            for (int i = 0; i < arrayType.Rank; i++)
                arrayGet.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));

            return arrayGet;
        }

        public static MethodReference ArrayAddress(ArrayType arrayType)
        {
            MethodReference arrayAddress = new MethodReference("Address", arrayType.Module.TypeSystem.Void, arrayType);
            arrayAddress.HasThis = true;
            arrayAddress.ReturnType = TypeReferenceRocks.MakeByReferenceType(arrayType.ElementType);
            for (int i = 0; i < arrayType.Rank; i++)
                arrayAddress.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));

            return arrayAddress;
        }

        public static MethodReference ArrayGet(ArrayType arrayType)
        {
            MethodReference arrayGet = new MethodReference("Get", arrayType.ElementType, arrayType);
            arrayGet.HasThis = true;
            for (int i = 0; i < arrayType.Rank; i++)
                arrayGet.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));

            return arrayGet;
        }
    }
}
