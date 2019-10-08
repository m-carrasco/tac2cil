using Model;
using Model.Types;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
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
                return CreateInterfaceDefinition(typeDefinition);
            }
            else if (typeDefinition.Kind == TypeDefinitionKind.Class)
            {
                return CreateClassDefinition(typeDefinition);
            }

            throw new NotImplementedException();
        }

        private Mono.Cecil.TypeDefinition CreateInterfaceDefinition(Model.Types.TypeDefinition typeDefinition)
        {
            var cecilDefinition = CreateClassDefinition(typeDefinition);
            cecilDefinition.Attributes |= TypeAttributes.Interface;
            cecilDefinition.Attributes |= TypeAttributes.Abstract;
            // todo: not sure about this
            cecilDefinition.Attributes &= ~TypeAttributes.Class;
            return cecilDefinition;
        }

        private TypeAttributes GetVisibility(Model.Types.VisibilityKind visibility)
        {
            /*
            // analysis-net maps NestedPublic and Public to VisibilityKind.Public
            // analysis-net maps NestedAssembly and NotPublic to VisibilityKind.Internal
            TypeAttributes res = 0;

            if (visibility.HasFlag(VisibilityKind.Private))
                res |= TypeAttributes.NestedPrivate;
            if (visibility.HasFlag(VisibilityKind.Public))
                res |= TypeAttributes.Public;
            if (visibility.HasFlag(VisibilityKind.Internal) && !visibility.HasFlag(VisibilityKind.Protected))
                res |= TypeAttributes.NotPublic;
            if (visibility.HasFlag(VisibilityKind.Protected) && !visibility.HasFlag(VisibilityKind.Internal))
                res |= TypeAttributes.NestedFamily;
            if (visibility.HasFlag(VisibilityKind.Protected) && !visibility.HasFlag(VisibilityKind.Internal))
                res |= TypeAttributes.NestedFamORAssem;

            return res;*/

            return TypeAttributes.Public;
        }

        private Mono.Cecil.TypeDefinition CreateClassDefinition(Model.Types.TypeDefinition typeDefinition)
        {
            string namespaceName = typeDefinition.ContainingNamespace.FullName;
            TypeReference baseType = typeDefinition.Base == null ? null : typeReferenceGenerator.GenerateTypeReference(typeDefinition.Base);
            TypeAttributes attributes = Mono.Cecil.TypeAttributes.Class | GetVisibility(typeDefinition.Visibility);

            // hack: an abstract class can have no abstract methods
            // there is no field in the type definition
            if (typeDefinition.Methods.Any(m => m.IsAbstract))
                attributes |= Mono.Cecil.TypeAttributes.Abstract;

            var t = new Mono.Cecil.TypeDefinition(namespaceName, typeDefinition.MetadataName(), attributes, baseType);

            CreateGenericParameters(typeDefinition, t);
            CreateMethodDefinitions(typeDefinition, t);
            CreateFieldDefinitions(typeDefinition, t);
            CreateInterfaceImplementations(typeDefinition, t);

            return t;
        }

        private void CreateGenericParameters(Model.Types.TypeDefinition typeDefinition, Mono.Cecil.TypeDefinition t)
        {
            var genericParameters = typeDefinition.GenericParameters.Select(p => new Mono.Cecil.GenericParameter(t));
            foreach (var gp in genericParameters)
            {
                t.GenericParameters.Add(gp);
                var constraints = typeDefinition.GenericParameters.ElementAt(gp.Position)
                    .Constraints.Select(c => new GenericParameterConstraint(typeReferenceGenerator.GenerateTypeReference(c)));
                gp.Constraints.AddRange(constraints);
            }
        }

        private void CreateMethodDefinitions(Model.Types.TypeDefinition typeDefinition, Mono.Cecil.TypeDefinition t)
        {
            foreach (var methodDefinition in typeDefinition.Methods)
            {
                MethodDefinitionGenerator methodDefinitionGen = new MethodDefinitionGenerator(methodDefinition, typeReferenceGenerator, t, module);
                t.Methods.Add(methodDefinitionGen.GenerateMethodDefinition());
            }
        }

        private void CreateFieldDefinitions(Model.Types.TypeDefinition typeDefinition, Mono.Cecil.TypeDefinition t)
        {
            foreach (var field in typeDefinition.Fields)
            {
                // analysis-net is not currently giving this information
                var fieldAttribute = FieldAttributes.Public;
                if (field.IsStatic)
                    fieldAttribute |= FieldAttributes.Static;

                TypeReference fieldType = field.Type is IGenericParameterReference genericReference && genericReference.GenericContainer == typeDefinition ?
                    t.GenericParameters.ElementAt(genericReference.Index)
                    : typeReferenceGenerator.GenerateTypeReference(field.Type);

                Mono.Cecil.FieldDefinition fieldDefinition = new Mono.Cecil.FieldDefinition(field.Name, fieldAttribute, fieldType);
                t.Fields.Add(fieldDefinition);
            }
        }

        private void CreateInterfaceImplementations(Model.Types.TypeDefinition typeDefinition, Mono.Cecil.TypeDefinition t)
        {
            foreach (var inter in typeDefinition.Interfaces)
                t.Interfaces.Add(new InterfaceImplementation(typeReferenceGenerator.GenerateTypeReference(inter)));
        }
    }

}
