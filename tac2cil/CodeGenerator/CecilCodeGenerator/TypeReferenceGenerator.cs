using Model;
using Model.Types;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace CodeGenerator.CecilCodeGenerator
{
    class TypeReferenceGenerator
    {
        private ModuleDefinition currentModule;
        private IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap;
        private Host host;
        public TypeReferenceGenerator(ModuleDefinition moduleDefinition, IDictionary<Model.Assembly, AssemblyDefinition> assembliesMap, Host host)
        {
            this.currentModule = moduleDefinition;
            this.assembliesMap = assembliesMap;
            this.host = host;
        }

        public TypeReference GenerateTypeReference(Model.Types.IBasicType basicType)
        {
            if (basicType.Equals(Model.Types.PlatformTypes.Object))
                return currentModule.TypeSystem.Object;

            if (basicType.Equals(Model.Types.PlatformTypes.Void))
                return currentModule.TypeSystem.Void;

            if (basicType.Equals(Model.Types.PlatformTypes.Int32))
                return currentModule.TypeSystem.Int32;

            ModuleDefinition moduleDefinition = basicType is Model.Types.ITypeDefinition typeDef ? ModuleDefinitionForTypeDefinition(typeDef) : null;
            IMetadataScope metadataScope = null;

            TypeReference typeReference = new TypeReference(basicType.ContainingNamespace, basicType.Name, moduleDefinition, metadataScope);

            return typeReference;
        }

        public TypeReference GenerateTypeReference(Model.Types.IType type)
        {
            if (type is IBasicType basicType)
            {
                //StructDefinition
                //EnumDefinition
                //InterfaceDefinition
                //ClassDefinition 
                //BasicType

                return GenerateTypeReference(basicType);
            }

            if (type is GenericParameterReference genericParameterReference)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.GenericParameter genericParameter)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.FunctionPointerType functionPointerType)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.PointerType pointerType)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.ArrayType arrayType)
            {
                throw new NotImplementedException();
            }

            if (type is UnknownType unknownType)
            {
                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        private ModuleDefinition ModuleDefinitionForTypeDefinition(ITypeDefinition typeDefinition)
        {
            // the type definition must be in some of the loaded assemblies
            // we are going to look for its containing module

            var containingAssembly = host.ResolveReference(typeDefinition.ContainingAssembly);
            Contract.Assert(containingAssembly != null);

            ModuleDefinition moduleDef = assembliesMap[containingAssembly].MainModule;
            Contract.Assert(moduleDef != null);

            return moduleDef;
        }
    }

}
