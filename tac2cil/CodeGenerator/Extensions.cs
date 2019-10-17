using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    public static class Extensions
    {
        public static string MetadataName(this Model.Types.IBasicType basicType)
        {
            int genericParameters = basicType.GenericParameterCount;

            // nested types
            if (basicType.ContainingType != null)
                genericParameters -= basicType.ContainingType.GenericParameterCount;

            if (genericParameters < 0)
                throw new Exception("calculated generic parameter count cannot be negative.");

            if (genericParameters == 0)
                return basicType.Name;

            return string.Format("{0}`{1}", basicType.Name, genericParameters);
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

        // before yielding type def A it yields every nested type in A
        // it could be improved by doing an iterative dfs
        public static IEnumerable<Model.Types.TypeDefinition> TraverseTypes(this Model.Assembly assembly)
        {
            ISet<Model.Types.TypeDefinition> visited = new HashSet<Model.Types.TypeDefinition>();
            
            foreach (var typeDefinition in Model.Types.TypeHelper.GetAllTypes(assembly.RootNamespace))
            {
                if (visited.Contains(typeDefinition))
                    continue;

                foreach (var t in DFS(typeDefinition, visited))
                    yield return t;
            }
        }

        private static LinkedList<Model.Types.TypeDefinition> DFS(Model.Types.TypeDefinition typeDefinition, ISet<Model.Types.TypeDefinition> visited)
        {
            visited.Add(typeDefinition);

            if (typeDefinition.ContainingType == null ||
                visited.Contains(typeDefinition.ContainingType))
            {
                var l = new LinkedList<Model.Types.TypeDefinition>();
                l.AddLast(typeDefinition);
                return l;
            }

            var rec = DFS(typeDefinition.ContainingType, visited);
            rec.AddLast(typeDefinition);

            return rec;
        }
    }
}
