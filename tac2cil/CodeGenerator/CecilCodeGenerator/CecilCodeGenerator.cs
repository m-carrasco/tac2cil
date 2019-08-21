using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        
        private Mono.Cecil.MethodDefinition GetMainDefinitionInCecilModule(ModuleDefinition module)
        {
            var mainQuery = from t in module.Types
                            from m in t.Methods
                            where m.IsStatic && m.Name.Equals("Main")
                            select m;

            var main = mainQuery.SingleOrDefault();

            return main;
        }

        private Model.Types.MethodDefinition GetMainDefinitionInAnalysisNetAssembly(Model.Assembly assembly)
        {
            var mainQuery = from t in assembly.RootNamespace.GetAllTypes()
                            from m in t.Members.OfType<Model.Types.MethodDefinition>()
                            where m.IsStatic && m.Name.Equals("Main")
                            select m;

            var main = mainQuery.SingleOrDefault();

            return main;
        }

        public void GenerateAssemblies(string pathToFolder)
        {
            foreach (var analysisNetAssembly in host.Assemblies)
            {
                string moduleName = analysisNetAssembly.Name;
                // todo: analysis-net does not give information about it
                // if there is a main method in it, we are updating this to console otherwise we flag it as a dll
                // not sure we can workaround it for windows forms
                ModuleKind moduleKind = GetMainDefinitionInAnalysisNetAssembly(analysisNetAssembly) != null ? ModuleKind.Console : ModuleKind.Dll;

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

                module.EntryPoint = GetMainDefinitionInCecilModule(module);
            }

            foreach (var assembly in assembliesMap.Values)
            {
                assembly.Write(Path.Combine(pathToFolder, assembly.Name.Name));
            }
        }
    }
}
