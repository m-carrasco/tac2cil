using Model.Types;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator.CecilCodeGenerator
{
    class TypeDefinitionGenerator
    {
        private ITypeDefinition def;
        private ModuleDefinition module;
        private TypeReferenceGenerator typeReferenceGenerator;

        public TypeDefinitionGenerator(Model.Types.ITypeDefinition def, ModuleDefinition module, TypeReferenceGenerator typeReferenceGenerator)
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
                    Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, typeReferenceGenerator.GenerateTypeReference(typeDef.Base));

                foreach (var methodDefinition in typeDef.Methods)
                {
                    MethodDefinitionGenerator methodDefinitionGen = new MethodDefinitionGenerator(methodDefinition, typeReferenceGenerator);
                    t.Methods.Add(methodDefinitionGen.GenerateMethodDefinition());
                }

                foreach (var inter in typeDef.Interfaces)
                    t.Interfaces.Add(new InterfaceImplementation(typeReferenceGenerator.GenerateTypeReference(inter)));

                return t;
            }

            throw new NotImplementedException();
        }
    }

}
