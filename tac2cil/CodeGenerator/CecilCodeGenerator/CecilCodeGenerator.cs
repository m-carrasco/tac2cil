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


        private void CreateEmptyAssemblies(DefinitionMapping definitionMapping)
        {
            IDictionary<Model.Assembly, AssemblyDefinition> map
                = definitionMapping.AssembliesMap;

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
            DefinitionMapping definitionMapping = new DefinitionMapping();
            CreateEmptyAssemblies(definitionMapping);
            CreateEmptyDefinitions(definitionMapping);
            CompleteDefinitions(definitionMapping);

            return definitionMapping.AssembliesMap.Values;

            /*
            IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap = new Dictionary<Model.Assembly, AssemblyDefinition>();
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

            return assembliesMap.Values;*/
        }

        // First we define empty definitions for types and methods (with their generic parameters)
        // In this way, the overall process for generating type/method references is simplified
        // We can directly return the definition as a reference.
        // One possible drawback is that the reference points to something that is not fully created.
        private void CreateEmptyDefinitions(DefinitionMapping definitionMapping)
        {
            var assembliesMap = definitionMapping.AssembliesMap;
            foreach (var keyval in assembliesMap)
            {
                var cecilAssembly = keyval.Value;
                var analysisNetAssembly = keyval.Key;

                ReferenceGenerator referenceGen = new ReferenceGenerator(new Context(cecilAssembly.MainModule, definitionMapping));
                DefinitionGenerator definitionGen = new DefinitionGenerator(referenceGen);

                foreach (var analysisNetType in analysisNetAssembly.RootNamespace.GetAllTypes())
                {
                    Mono.Cecil.TypeDefinition emptyType = definitionGen.CreateEmptyTypeDefinition(analysisNetType);
                    cecilAssembly.MainModule.Types.Add(emptyType);

                    foreach (var analysisNetMethod in analysisNetType.Methods)
                    {
                        var emptyMethod = definitionGen.CreateEmptyMethodDefinition(analysisNetMethod);
                        emptyType.Methods.Add(emptyMethod);
                    }
                }
            }

            CreateFieldDefinitions(definitionMapping);
        }

        private void CreateFieldDefinitions(DefinitionMapping definitionMapping)
        {
            var assembliesMap = definitionMapping.AssembliesMap;
            foreach (var keyval in assembliesMap)
            {
                var cecilAssembly = keyval.Value;
                var analysisNetAssembly = keyval.Key;

                ReferenceGenerator referenceGen = new ReferenceGenerator(new Context(cecilAssembly.MainModule, definitionMapping));
                DefinitionGenerator definitionGen = new DefinitionGenerator(referenceGen);

                foreach (var analysisNetType in analysisNetAssembly.RootNamespace.GetAllTypes())
                {
                    var cecilType = definitionMapping.TypesMap[analysisNetType];

                    foreach (var analysisNetField in analysisNetType.Fields)
                    {
                        var cecilField = definitionGen.CreateFieldDefinition(analysisNetField);
                        definitionMapping.FieldsMap[analysisNetField] = cecilField;
                        cecilType.Fields.Add(cecilField);
                    }
                }
            }
        }

        private void CompleteDefinitions(DefinitionMapping definitionMapping)
        {
            var assembliesMap = definitionMapping.AssembliesMap;
            foreach (var keyval in assembliesMap)
            {
                var cecilAssembly = keyval.Value;
                var analysisNetAssembly = keyval.Key;

                ReferenceGenerator referenceGen = new ReferenceGenerator(new Context(cecilAssembly.MainModule, definitionMapping));
                DefinitionGenerator definitionGen = new DefinitionGenerator(referenceGen);

                foreach (var analysisNetType in analysisNetAssembly.RootNamespace.GetAllTypes())
                {
                    definitionGen.CompleteTypeDefinition(analysisNetType);

                    foreach (var analysisNetMethod in analysisNetType.Methods)
                        definitionGen.CompleteMethodDefinition(analysisNetMethod);
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
