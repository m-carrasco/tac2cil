using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    public static class Extensions
    {
        public static string MetadataName(this Model.Types.IBasicType basicType)
        {
            if (basicType.GenericParameterCount == 0 && basicType.GenericArguments.Count == 0)
                return basicType.Name;

            int number = Math.Max(basicType.GenericParameterCount, basicType.GenericArguments.Count);

            return string.Format("{0}`{1}", basicType.Name, number);
        }

        public static void AddRange<T>(this ICollection<T> t, IEnumerable<T> x)
        {
            foreach (var elem in x)
                t.Add(elem);
        }

        public static void CreateGenericParameters(this Mono.Cecil.IGenericParameterProvider container, int count)
        {
            for (int i = 0; i < count; i++)
                container.GenericParameters.Add(new Mono.Cecil.GenericParameter(container));
        }
    }
}
