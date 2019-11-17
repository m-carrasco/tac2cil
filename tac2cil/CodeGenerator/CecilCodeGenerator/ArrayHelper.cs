using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace CodeGenerator
{
    internal class ArrayHelper
    {
        public static MethodReference ArrayCtor(ArrayType arrayType)
        {
            MethodReference arrayCtor = new MethodReference(".ctor", arrayType.Module.TypeSystem.Void, arrayType)
            {
                HasThis = true
            };
            for (int i = 0; i < arrayType.Rank; i++)
            {
                arrayCtor.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));
            }

            return arrayCtor;
        }

        public static MethodReference ArraySet(ArrayType arrayType)
        {
            MethodReference arrayGet = new MethodReference("Set", arrayType.Module.TypeSystem.Void, arrayType)
            {
                HasThis = true
            };
            for (int i = 0; i < arrayType.Rank; i++)
            {
                arrayGet.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));
            }

            arrayGet.Parameters.Add(new ParameterDefinition(arrayType.ElementType));
            return arrayGet;
        }

        public static MethodReference ArrayAddress(ArrayType arrayType)
        {
            MethodReference arrayAddress = new MethodReference("Address", arrayType.Module.TypeSystem.Void, arrayType)
            {
                HasThis = true,
                ReturnType = TypeReferenceRocks.MakeByReferenceType(arrayType.ElementType)
            };
            for (int i = 0; i < arrayType.Rank; i++)
            {
                arrayAddress.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));
            }

            return arrayAddress;
        }

        public static MethodReference ArrayGet(ArrayType arrayType)
        {
            MethodReference arrayGet = new MethodReference("Get", arrayType.ElementType, arrayType)
            {
                HasThis = true
            };
            for (int i = 0; i < arrayType.Rank; i++)
            {
                arrayGet.Parameters.Add(new ParameterDefinition(arrayType.Module.TypeSystem.Int32));
            }

            return arrayGet;
        }
    }
}
