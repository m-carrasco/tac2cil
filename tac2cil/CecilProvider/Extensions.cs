using System;
using System.Collections.Generic;
using System.Text;
using Cecil = Mono.Cecil;

namespace CecilProvider
{
    public static class Extensions
    {
        public static void AddRange<T>(this ICollection<T> t, IEnumerable<T> x)
        {
            foreach (var elem in x)
                t.Add(elem);
        }

        // before yielding type def A it yields every nested type in A
        // it could be improved by doing an iterative dfs
        public static IEnumerable<Cecil.TypeDefinition> TraverseTypes(this Cecil.ModuleDefinition module)
        {
            ISet<Cecil.TypeDefinition> visited = new HashSet<Cecil.TypeDefinition>();

            foreach (var typeDefinition in module.GetTypes())
            {
                if (visited.Contains(typeDefinition))
                    continue;

                foreach (var t in DFS(typeDefinition, visited))
                    yield return t;
            }
        }

        private static LinkedList<Cecil.TypeDefinition> DFS(Cecil.TypeDefinition typeDefinition, ISet<Cecil.TypeDefinition> visited)
        {
            visited.Add(typeDefinition);

            if (typeDefinition.DeclaringType == null ||
                visited.Contains(typeDefinition.DeclaringType))
            {
                var l = new LinkedList<Cecil.TypeDefinition>();
                l.AddLast(typeDefinition);
                return l;
            }

            var rec = DFS(typeDefinition.DeclaringType, visited);
            rec.AddLast(typeDefinition);

            return rec;
        }
    }
}
