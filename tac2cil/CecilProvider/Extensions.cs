using System.Collections.Generic;
using Cecil = Mono.Cecil;

namespace CecilProvider
{
    public static class Extensions
    {
        public static void AddRange<T>(this ICollection<T> t, IEnumerable<T> x)
        {
            foreach (T elem in x)
            {
                t.Add(elem);
            }
        }

        // before yielding type def A it yields every nested type in A
        // it could be improved by doing an iterative dfs
        public static IEnumerable<Cecil.TypeDefinition> TraverseTypes(this Cecil.ModuleDefinition module)
        {
            ISet<Cecil.TypeDefinition> visited = new HashSet<Cecil.TypeDefinition>();

            foreach (Cecil.TypeDefinition typeDefinition in module.GetTypes())
            {
                if (visited.Contains(typeDefinition))
                {
                    continue;
                }

                foreach (Cecil.TypeDefinition t in DFS(typeDefinition, visited))
                {
                    yield return t;
                }
            }
        }

        private static LinkedList<Cecil.TypeDefinition> DFS(Cecil.TypeDefinition typeDefinition, ISet<Cecil.TypeDefinition> visited)
        {
            visited.Add(typeDefinition);

            if (typeDefinition.DeclaringType == null ||
                visited.Contains(typeDefinition.DeclaringType))
            {
                LinkedList<Cecil.TypeDefinition> l = new LinkedList<Cecil.TypeDefinition>();
                l.AddLast(typeDefinition);
                return l;
            }

            LinkedList<Cecil.TypeDefinition> rec = DFS(typeDefinition.DeclaringType, visited);
            rec.AddLast(typeDefinition);

            return rec;
        }
    }
}
