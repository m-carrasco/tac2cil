﻿using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;

namespace CecilProvider
{
    internal class AssemblyExtractor
    {
        private readonly Cecil.ModuleDefinition module;
        private readonly IDictionary<string, AnalysisNet.Namespace> namespaces;
        private readonly AnalysisNet.Host host;
        private readonly IDictionary<Cecil.TypeDefinition, AnalysisNet.Types.TypeDefinition> typeDefinitions;
        public AssemblyExtractor(Cecil.ModuleDefinition module, AnalysisNet.Host host)
        {
            this.module = module;
            namespaces = new Dictionary<string, AnalysisNet.Namespace>();
            this.host = host;
            typeDefinitions = new Dictionary<Cecil.TypeDefinition, AnalysisNet.Types.TypeDefinition>();
        }
        public AnalysisNet.Assembly ExtractAssembly()
        {
            // create empty assembly
            AnalysisNet.Assembly assembly = new AnalysisNet.Assembly(module.Assembly.Name.Name);

            // populate assembly references
            assembly.References.AddRange(ExtractAssemblyReferences());

            // create root namespace
            // every other namespace is created while processing each cecil type definition
            assembly.RootNamespace = new AnalysisNet.Namespace(string.Empty)
            {
                ContainingAssembly = new AnalysisNet.AssemblyReference(assembly.Name)
            };
            namespaces[string.Empty] = assembly.RootNamespace;

            // re use the same object because it contains a cache inside
            // but be sure typeExtractor is not referenced forever
            // otherwise we would not dipose cecil code model
            TypeExtractor typeExtractor = new TypeExtractor(host);

            // if cecilType is a nested type, we guarantee that we have already visited its declaring type
            foreach (Cecil.TypeDefinition cecilType in module.TraverseTypes())
            {
                ExtractTypeDefinition(cecilType, assembly, typeExtractor);
            }

            return assembly;
        }

        // the extracted type is added to the expected namespace
        // if cecilType is a nested type, we guarantee that we have already visited its declaring type
        private void ExtractTypeDefinition(Cecil.TypeDefinition cecilType, AnalysisNet.Assembly assembly, TypeExtractor typeExtractor)
        {
            // afaik it is not necessary to generate this class
            // for instance cci does not even load it although cecil does 
            if (cecilType.Name.Equals("<Module>") &&
                cecilType.BaseType == null)
            {
                return;
            }

            AnalysisNet.Types.TypeDefinition extractedType = typeExtractor.ExtractTypeDefinition(cecilType);
            typeDefinitions[cecilType] = extractedType;

            extractedType.ContainingAssembly = assembly;

            // analysis-net does not follow ecma standard for nested types
            // analysis-net expects to have nested types in their ContainingType.Types and share the same namespace that its enclosing type.
            // However, nested types should not be added to the ContainingNamespace.Types
            // If the type is not nested then the processed type is added to its namespace directly
            if (cecilType.DeclaringType != null)
            {
                AnalysisNet.Types.TypeDefinition containingType = typeDefinitions[cecilType.DeclaringType];
                extractedType.ContainingType = containingType;
                containingType.Types.Add(extractedType);
                extractedType.ContainingNamespace = containingType.ContainingNamespace;
            }
            else
            {
                AnalysisNet.Namespace ns = GetOrCreateNamespace(cecilType.Namespace);
                extractedType.ContainingNamespace = ns;
                ns.Types.Add(extractedType);
            }
        }

        private IEnumerable<AnalysisNet.IAssemblyReference> ExtractAssemblyReferences()
        {
            Mono.Collections.Generic.Collection<Cecil.AssemblyNameReference> cecilReferences = module.AssemblyReferences;
            IEnumerable<AnalysisNet.AssemblyReference> result = cecilReferences.Select(r => new AnalysisNet.AssemblyReference(r.Name));
            return result;
        }

        private AnalysisNet.Namespace GetOrCreateNamespace(string nsFullName)
        {
            if (namespaces.TryGetValue(nsFullName, out AnalysisNet.Namespace res))
            {
                return res;
            }

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
                parentFullName = string.Empty;
                currentName = nsFullName;
            }

            AnalysisNet.Namespace parentNamespace = GetOrCreateNamespace(parentFullName);
            currentNamespace = new AnalysisNet.Namespace(currentName)
            {
                ContainingNamespace = parentNamespace
            };
            parentNamespace.Namespaces.Add(currentNamespace);

            namespaces[nsFullName] = currentNamespace;

            return currentNamespace;
        }
    }
}
