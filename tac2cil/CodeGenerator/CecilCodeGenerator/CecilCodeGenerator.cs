using Model.Types;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeGenerator.CecilCodeGenerator
{
    public class CecilCodeGenerator : ICodeGenerator<AssemblyDefinition>
    {
        private readonly Model.Host host;
        public CecilCodeGenerator(Model.Host h)
        {
            host = h;
        }

        public Model.Host Host => host;

        private Mono.Cecil.MethodDefinition GetMainDefinitionInCecilModule(ModuleDefinition module)
        {
            IEnumerable<Mono.Cecil.MethodDefinition> mainQuery = from t in module.Types
                                                                 from m in t.Methods
                                                                 where m.IsStatic && m.Name.Equals("Main")
                                                                 select m;

            Mono.Cecil.MethodDefinition main = mainQuery.SingleOrDefault();

            return main;
        }

        private Model.Types.MethodDefinition GetMainDefinitionInAnalysisNetAssembly(Model.Assembly assembly)
        {
            IEnumerable<Model.Types.MethodDefinition> mainQuery = from t in assembly.RootNamespace.GetAllTypes()
                                                                  from m in t.Members.OfType<Model.Types.MethodDefinition>()
                                                                  where m.IsStatic && m.Name.Equals("Main")
                                                                  select m;

            Model.Types.MethodDefinition main = mainQuery.SingleOrDefault();

            return main;
        }
        private void CreateEmptyAssemblies(ModelMapping modelMapping)
        {
            IDictionary<Model.Assembly, AssemblyDefinition> map
                = modelMapping.AssembliesMap;

            foreach (Model.Assembly analysisNetAssembly in host.Assemblies)
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
            IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap = modelMapping.AssembliesMap;
            foreach (KeyValuePair<Model.Assembly, AssemblyDefinition> keyval in assembliesMap)
            {
                AssemblyDefinition cecilAssembly = keyval.Value;
                Model.Assembly analysisNetAssembly = keyval.Key;

                ReferenceGenerator referenceGen = new ReferenceGenerator(new Context(cecilAssembly.MainModule, modelMapping, Host));

                // TraverseTypes returns every nested type in A before returning A
                // this is assumed by the TypeGenerator and MethodGenerator
                foreach (Model.Types.TypeDefinition analysisNetType in analysisNetAssembly.TraverseTypes())
                {
                    TypeGenerator typeGenerator = new TypeGenerator(referenceGen);
                    Mono.Cecil.TypeDefinition cecilTypeDef = typeGenerator.TypeDefinition(analysisNetType);

                    // nested types are not added directly to the main module
                    // instead they are added to their enclosing type (that's the way cecil works)
                    if (cecilTypeDef.DeclaringType == null)
                    {
                        cecilAssembly.MainModule.Types.Add(cecilTypeDef);
                    }

                    foreach (Model.Types.MethodDefinition analysisNetMethod in analysisNetType.Methods)
                    {
                        MethodGenerator methodGenerator = new MethodGenerator(referenceGen);
                        cecilTypeDef.Methods.Add(methodGenerator.MethodDefinition(analysisNetMethod));
                    }

                    // we need to have every method definition created
                    typeGenerator.PropertyDefinitions(analysisNetType, cecilTypeDef);
                }
            }
        }
        public void WriteAssemblies(string pathToFolder)
        {
            ICollection<AssemblyDefinition> assemblies = GenerateAssemblies();

            foreach (AssemblyDefinition assembly in assemblies)
            {
                string name = assembly.Name.Name + (assembly.MainModule.Kind == ModuleKind.Dll ? ".dll" : ".exe");
                assembly.Write(Path.Combine(pathToFolder, name));
            }
        }
    }
}
