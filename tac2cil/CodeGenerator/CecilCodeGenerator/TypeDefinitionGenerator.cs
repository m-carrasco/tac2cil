using Model.Types;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator.CecilCodeGenerator
{
    class TypeDefinitionGenerator
    {
        private Model.Types.TypeDefinition def;
        private ModuleDefinition module;
        private TypeReferenceGenerator typeReferenceGenerator;

        public TypeDefinitionGenerator(Model.Types.TypeDefinition def, ModuleDefinition module, TypeReferenceGenerator typeReferenceGenerator)
        {
            this.def = def;
            this.module = module;
            this.typeReferenceGenerator = typeReferenceGenerator;
        }

        public Mono.Cecil.TypeDefinition Generate()
        {
            Model.Types.TypeDefinition typeDefinition = def;
            if (typeDefinition.Kind == TypeDefinitionKind.Struct)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition.Kind == TypeDefinitionKind.Enum)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition.Kind == TypeDefinitionKind.Interface)
            {
                throw new NotImplementedException();
            }
            else if (typeDefinition.Kind == TypeDefinitionKind.Class)
            {
                string namespaceName = typeDefinition.ContainingNamespace.FullName;
                var t = new Mono.Cecil.TypeDefinition(namespaceName, typeDefinition.Name,
                    Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public,  typeDefinition.Base == null ? null : typeReferenceGenerator.GenerateTypeReference(typeDefinition.Base));

                foreach (var methodDefinition in typeDefinition.Methods)
                {
                    MethodDefinitionGenerator methodDefinitionGen = new MethodDefinitionGenerator(methodDefinition, typeReferenceGenerator, t);
                    t.Methods.Add(methodDefinitionGen.GenerateMethodDefinition());
                }

                foreach (var field in typeDefinition.Fields)
                {
                    // analysis-net is not currently giving this information
                    var fieldAttribute = FieldAttributes.Public;
                    if (field.IsStatic)
                        fieldAttribute |= FieldAttributes.Static;

                    Mono.Cecil.FieldDefinition fieldDefinition = new Mono.Cecil.FieldDefinition(field.Name, fieldAttribute, typeReferenceGenerator.GenerateTypeReference(field.Type));
                    t.Fields.Add(fieldDefinition);
                }

                foreach (var inter in typeDefinition.Interfaces)
                    t.Interfaces.Add(new InterfaceImplementation(typeReferenceGenerator.GenerateTypeReference(inter)));

                return t;
            }

            throw new NotImplementedException();
        }
    }

}
