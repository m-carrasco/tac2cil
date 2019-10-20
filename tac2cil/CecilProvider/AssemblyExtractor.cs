using System;
using System.Collections.Generic;
using System.Text;
using Cecil = Mono.Cecil;
using AnalysisNet = Model;
using System.Linq;

namespace CecilProvider
{
    class AssemblyExtractor
    {
        private Cecil.ModuleDefinition module;
        private IDictionary<string, AnalysisNet.Namespace> namespaces;
        private AnalysisNet.Host host;
        private IDictionary<Cecil.TypeDefinition, AnalysisNet.Types.TypeDefinition> typeDefinitions;
        public AssemblyExtractor(Cecil.ModuleDefinition module, AnalysisNet.Host host)
        {
            this.module = module;
            this.namespaces = new Dictionary<string, AnalysisNet.Namespace>();
            this.host = host;
            this.typeDefinitions = new Dictionary<Cecil.TypeDefinition, AnalysisNet.Types.TypeDefinition>();
        }
        public AnalysisNet.Assembly ExtractAssembly()
        {
            // create empty assembly
            AnalysisNet.Assembly assembly = new AnalysisNet.Assembly(module.Assembly.Name.Name);
            
            // populate assembly references
            assembly.References.AddRange(ExtractAssemblyReferences());

            // create root namespace
            // every other namespace is created while processing each cecil type definition
            assembly.RootNamespace = new AnalysisNet.Namespace(String.Empty)
            {
                ContainingAssembly = new AnalysisNet.AssemblyReference(assembly.Name)
            };
            namespaces[String.Empty] = assembly.RootNamespace;

            // if cecilType is a nested type, we guarantee that we have already visited its declaring type
            foreach (var cecilType in module.TraverseTypes())
                ExtractTypeDefinition(cecilType, assembly);
            
            return assembly;
        }

        // the extracted type is added to the expected namespace
        // if cecilType is a nested type, we guarantee that we have already visited its declaring type
        private void ExtractTypeDefinition(Cecil.TypeDefinition cecilType, AnalysisNet.Assembly assembly)
        {
            // afaik it is not necessary to generate this class
            // for instance cci does not even load it although cecil does 
            if (cecilType.Name.Equals("<Module>") &&
                cecilType.BaseType == null)
                return;

            TypeExtractor typeExtractor = new TypeExtractor(host);
            var extractedType = typeExtractor.ExtractTypeDefinition(cecilType);
            typeDefinitions[cecilType] = extractedType;

            extractedType.ContainingAssembly = assembly;

            // analysis-net does not follow ecma standard for nested types
            // analysis-net expects to have nested types in their ContainingType.Types and share the same namespace that its enclosing type.
            // However, nested types should not be added to the ContainingNamespace.Types
            // If the type is not nested then the processed type is added to its namespace directly
            if (cecilType.DeclaringType != null)
            {
                var containingType = typeDefinitions[cecilType.DeclaringType];
                extractedType.ContainingType = containingType;
                containingType.Types.Add(extractedType);
                extractedType.ContainingNamespace = containingType.ContainingNamespace;
            }
            else
            {
                var ns = GetOrCreateNamespace(cecilType.Namespace);
                extractedType.ContainingNamespace = ns;
                ns.Types.Add(extractedType);
            }

            //throw new NotImplementedException();
        }

        private IEnumerable<AnalysisNet.IAssemblyReference> ExtractAssemblyReferences()
        {
            var cecilReferences = module.AssemblyReferences;
            var result = cecilReferences.Select(r => new AnalysisNet.AssemblyReference(r.Name));
            return result;
        }

        private AnalysisNet.Namespace GetOrCreateNamespace(string nsFullName){
            if (namespaces.TryGetValue(nsFullName, out AnalysisNet.Namespace res))
                return res;

            AnalysisNet.Namespace currentNamespace;

            string parentFullName;
            string currentName;
            int lastOcurrence = nsFullName.LastIndexOf('.');
            if (lastOcurrence != -1)
            {
                // don't include the .
                parentFullName = nsFullName.Substring(0, lastOcurrence);
                currentName = nsFullName.Substring(lastOcurrence + 1, nsFullName.Count() - lastOcurrence - 1);
            }
            else
            {
                parentFullName = String.Empty;
                currentName = nsFullName;
            }

            AnalysisNet.Namespace parentNamespace = GetOrCreateNamespace(parentFullName);
            currentNamespace = new AnalysisNet.Namespace(currentName);
            currentNamespace.ContainingNamespace = parentNamespace;
            parentNamespace.Namespaces.Add(currentNamespace);

            namespaces[nsFullName] = currentNamespace;

            return currentNamespace;
        }
    }
}
