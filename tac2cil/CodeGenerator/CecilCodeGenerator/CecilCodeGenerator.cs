using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Model.Types;
using Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    public class CecilCodeGenerator : ICodeGenerator<AssemblyDefinition>
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
        private void CreateEmptyAssemblies(ModelMapping modelMapping)
        {
            IDictionary<Model.Assembly, AssemblyDefinition> map
                = modelMapping.AssembliesMap;

            foreach (var analysisNetAssembly in host.Assemblies)
            {
                string moduleName = analysisNetAssembly.Name;
                // todo: analysis-net does not give information about it
                // if there is a main method in it, we are updating this to console otherwise we flag it as a dll
                // not sure we can workaround it for windows forms
                ModuleKind moduleKind = GetMainDefinitionInAnalysisNetAssembly(analysisNetAssembly) != null ? ModuleKind.Console : ModuleKind.Dll;

                AssemblyDefinition cecilAssembly = AssemblyDefinition.CreateAssembly(
                    new AssemblyNameDefinition(analysisNetAssembly.Name, new Version(1, 0, 0, 0)), moduleName, moduleKind);

                map[analysisNetAssembly] = cecilAssembly;
            }
        }
        public ICollection<AssemblyDefinition> GenerateAssemblies()
        {
            ModelMapping modelMapping = new ModelMapping();
            CreateEmptyAssemblies(modelMapping);
            CreateDefinitions(modelMapping);

            return modelMapping.AssembliesMap.Values;
        }

        private void CreateDefinitions(ModelMapping modelMapping)
        {
            var assembliesMap = modelMapping.AssembliesMap;
            foreach (var keyval in assembliesMap)
            {
                var cecilAssembly = keyval.Value;
                var analysisNetAssembly = keyval.Key;

                ReferenceGenerator referenceGen = new ReferenceGenerator(new Context(cecilAssembly.MainModule, modelMapping));

                // TraverseTypes returns every nested type in A before returning A
                // this is assumed by the TypeGenerator and MethodGenerator
                foreach (var analysisNetType in analysisNetAssembly.TraverseTypes())
                {
                    TypeGenerator typeGenerator = new TypeGenerator(referenceGen);
                    var cecilTypeDef = typeGenerator.TypeDefinition(analysisNetType);

                    // nested types are not added directly to the main module
                    // instead they are added to their enclosing type (that's the way cecil works)
                    if (cecilTypeDef.DeclaringType == null)
                        cecilAssembly.MainModule.Types.Add(cecilTypeDef);

                    foreach (var analysisNetMethod in analysisNetType.Methods)
                    {
                        MethodGenerator methodGenerator = new MethodGenerator(referenceGen);
                        cecilTypeDef.Methods.Add(methodGenerator.MethodDefinition(analysisNetMethod));
                    }
                }
            }
        }
        public void WriteAssemblies(string pathToFolder)
        {
            var assemblies = GenerateAssemblies();

            foreach (var assembly in assemblies)
            {
                assembly.Write(Path.Combine(pathToFolder, assembly.Name.Name));
            }
        }
    }
}
