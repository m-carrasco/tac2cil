using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Model;
using Model.Types;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CodeGenerator
{
    public class CodeGenerator : ICodeGenerator
    {
        private readonly Model.Host host;
        public CodeGenerator(Model.Host h)
        {
            host = h;
        }

        public Model.Host Host
        {
            get { return host; }
        }

        private IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap = 
            new Dictionary<Model.Assembly, AssemblyDefinition>();

        private  ModuleDefinition ModuleDefinitionForTypeDefinition(ITypeDefinition typeDefinition)
        {
            // the type definition must be in some of the loaded assemblies
            // we are going to look for its containing module

            var containingAssembly = host.ResolveReference(typeDefinition.ContainingAssembly);
            Contract.Assert(containingAssembly != null);

            ModuleDefinition moduleDef = assembliesMap[containingAssembly].MainModule;
            Contract.Assert(moduleDef != null);

            return moduleDef;
        }
        private TypeReference TypeReferenceGenerator(Model.Types.IBasicType basicType, ModuleDefinition currentModule)
        {
            if (basicType.Equals(Model.Types.PlatformTypes.Object))
                return currentModule.TypeSystem.Object;

            ModuleDefinition moduleDefinition = basicType is Model.Types.ITypeDefinition typeDef ? ModuleDefinitionForTypeDefinition(typeDef) : null;
            IMetadataScope metadataScope = null;

            TypeReference typeReference = new TypeReference(basicType.ContainingNamespace, basicType.Name, moduleDefinition, metadataScope);

            return typeReference;
        }
        
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

                foreach (var analysisNetType in analysisNetAssembly.RootNamespace.GetAllTypes())
                {
                    TypeDefinitionGenerator typeDefGen = new TypeDefinitionGenerator(analysisNetType, module, TypeReferenceGenerator);
                    module.Types.Add(typeDefGen.Generate());
                }
            }

            foreach (var assembly in assembliesMap.Values)
            {
                assembly.Write(Path.Combine(pathToFolder, assembly.Name.Name));
            }
        }
    }

    class TypeDefinitionGenerator
    {
        private ITypeDefinition def;
        private ModuleDefinition module;
        private Func<IBasicType, ModuleDefinition, TypeReference> typeReferenceGenerator;

        public TypeDefinitionGenerator(Model.Types.ITypeDefinition def, ModuleDefinition module,  Func<IBasicType, ModuleDefinition, TypeReference> typeReferenceGenerator)
        {
            this.def = def;
            this.module = module;
            this.typeReferenceGenerator = typeReferenceGenerator;
        }

        public TypeDefinition Generate()
        {
            ITypeDefinition typeDefinition = def;

            if (typeDefinition is Model.Types.StructDefinition structDef)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition is Model.Types.EnumDefinition enumDef)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition is Model.Types.InterfaceDefinition interfaceDef)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition is Model.Types.ClassDefinition typeDef)
            {
                string namespaceName = typeDef.ContainingNamespace.ContainingNamespace.Name;
                var t = new TypeDefinition(namespaceName, typeDef.Name,
                    Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, typeReferenceGenerator(typeDef.Base, module));

                /*var ctor = new Mono.Cecil.MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig
                    | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, module.TypeSystem.Void);

                // create the constructor's method body
                var il = ctor.Body.GetILProcessor();

                il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ldarg_0));

                // call the base constructor
                il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Call, module.ImportReference(typeof(object).GetConstructor(Array.Empty<Type>()))));

                il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Nop));
                il.Append(il.Create(Mono.Cecil.Cil.OpCodes.Ret));

                t.Methods.Add(ctor);*/

                foreach (var inter in typeDef.Interfaces)
                    t.Interfaces.Add(new InterfaceImplementation(typeReferenceGenerator(inter, module)));

                return t;
            }

            throw new NotImplementedException();
        }
    }
}
