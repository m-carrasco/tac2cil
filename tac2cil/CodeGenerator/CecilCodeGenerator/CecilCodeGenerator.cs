using System;
using System.Collections.Generic;
using System.IO;
using Model.Types;
using Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    public class CecilCodeGenerator : ICodeGenerator
    {
        private readonly Model.Host host;
        public CecilCodeGenerator(Model.Host h)
        {
            host = h;
        }

        public Model.Host Host
        {
            get { return host; }
        }

        private IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap = 
            new Dictionary<Model.Assembly, AssemblyDefinition>();
        
        public void GenerateAssemblies(string pathToFolder)
        {
            foreach (var analysisNetAssembly in host.Assemblies)
            {
                string moduleName = analysisNetAssembly.Name;
                ModuleKind moduleKind = ModuleKind.Dll;

                AssemblyDefinition cecilAssembly = AssemblyDefinition.CreateAssembly(
                    new AssemblyNameDefinition(analysisNetAssembly.Name, new Version(1, 0, 0, 0)), moduleName, moduleKind);

                assembliesMap[analysisNetAssembly] = cecilAssembly;
            }

            foreach (var keyval in assembliesMap)
            {
                var cecilAssembly = keyval.Value;
                var analysisNetAssembly = keyval.Key;

                ModuleDefinition module = cecilAssembly.MainModule;

                TypeReferenceGenerator typeReferenceGenerator = new TypeReferenceGenerator(module, assembliesMap, host);

                foreach (var analysisNetType in analysisNetAssembly.RootNamespace.GetAllTypes())
                {
                    TypeDefinitionGenerator typeDefGen = new TypeDefinitionGenerator(analysisNetType, module, typeReferenceGenerator);
                    module.Types.Add(typeDefGen.Generate());
                }
            }

            foreach (var assembly in assembliesMap.Values)
            {
                assembly.Write(Path.Combine(pathToFolder, assembly.Name.Name));
            }
        }
    }
}
