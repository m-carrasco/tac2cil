using Model;
using Model.Types;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

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
        
        private string TypeNameForExternTypes(IBasicType basicType)
        {
            if (basicType.GenericParameterCount == 0)
                return basicType.Name;

            var arguments = string.Empty;

            if (basicType.GenericArguments.Count > 0)
            {
                arguments = string.Join(", ", basicType.GenericArguments);
                arguments = string.Format("<{0}>", arguments);
            }

            return string.Format("{0}`{1}{2}", basicType.Name, basicType.GenericParameterCount, arguments);
        }
        

        // todo: move to another class or change name of the class
        public Mono.Cecil.MethodReference GenerateMethodReference(Model.Types.IMethodReference method)
        {
            var containingType = this.GenerateTypeReference(method.ContainingType);

            var methodReference = new Mono.Cecil.MethodReference(method.Name,
                currentModule.TypeSystem.Void,
                containingType);

            methodReference.GenericParameters.AddRange(Enumerable.Repeat(new Mono.Cecil.GenericParameter(methodReference), method.GenericParameterCount));

            TypeReference returnType;
            if (method.ReturnType is IGenericParameterReference genericParameterReference && genericParameterReference.GenericContainer == method)
                returnType = methodReference.GenericParameters.ElementAt(genericParameterReference.Index);
            else
                returnType = this.GenerateTypeReference(method.ReturnType);

            methodReference.ReturnType = returnType;

            foreach (var param in method.Parameters)
            {
                TypeReference paramType;

                if (param.Type is Model.Types.IGenericParameterReference genericParameterReferenceParam && genericParameterReferenceParam.GenericContainer == method)
                    paramType = methodReference.GenericParameters.ElementAt(genericParameterReferenceParam.Index);
                else
                    paramType = this.GenerateTypeReference(param.Type);

                methodReference.Parameters.Add(new ParameterDefinition(paramType));
            }

            if (!method.IsStatic)
                methodReference.HasThis = true;

            if (method.GenericArguments.Count > 0)
            {
                var genericInstance = new GenericInstanceMethod(methodReference);
                foreach (var arg in method.GenericArguments)
                {
                    if (arg is Model.Types.IGenericParameterReference genericParameterReferenceParam && genericParameterReferenceParam.GenericContainer == method)
                        genericInstance.GenericArguments.Add(methodReference.GenericParameters.ElementAt(genericParameterReferenceParam.Index));
                    else
                        genericInstance.GenericArguments.Add(GenerateTypeReference(arg));
                }

                methodReference = genericInstance;
            } 

            methodReference = currentModule.ImportReference(methodReference);
            return methodReference;
        }

        public TypeReference GenerateTypeReference(Model.Types.IBasicType basicType)
        {
            if (basicType.Equals(Model.Types.PlatformTypes.Object))
                return currentModule.TypeSystem.Object;

            if (basicType.Equals(Model.Types.PlatformTypes.Void))
                return currentModule.TypeSystem.Void;

            if (basicType.Equals(Model.Types.PlatformTypes.Int32))
                return currentModule.TypeSystem.Int32;

            if (basicType.Equals(Model.Types.PlatformTypes.String))
                return currentModule.TypeSystem.String;

            
            Model.Types.TypeDefinition def = host.ResolveReference(basicType);

            TypeReference typeReference = null;
            // is a reference to a type in an assembly loaded by analysis-net?
            if (def != null) // yes, it is loaded by analysis-net
            {
                ModuleDefinition moduleDefinition = ModuleDefinitionForTypeDefinition(def); // get mono module for the analysis-net assembly
                IMetadataScope metadataScope = null;
                //IMetadataScope metadataScope = moduleDefinition; not sure if we should do this
                typeReference = new TypeReference(basicType.ContainingNamespace, basicType.Name, moduleDefinition, metadataScope);

                if (moduleDefinition != currentModule)
                    typeReference = currentModule.ImportReference(typeReference);
            }
            else // it is not loaded by analysis-net
            {
                if (basicType.ContainingAssembly.Name.Equals("mscorlib"))
                {
                    IMetadataScope metadataScope = currentModule.TypeSystem.CoreLibrary;
                    typeReference = new TypeReference(basicType.ContainingNamespace, TypeNameForExternTypes(basicType), null, metadataScope);
                    typeReference = currentModule.ImportReference(typeReference);
                }
                else
                {
                    // this is a reference to a type in a assembly that we don't know much about it.
                    // i guess we should create an implementation of the IMetadataScope based on the information given by analysis-net

                    throw new NotImplementedException();
                }
            }

            if (basicType.GenericParameterCount > 0)
            {
                if (basicType.GenericArguments.Count == 0)
                {
                    var genericParameters = Enumerable.Repeat(new Mono.Cecil.GenericParameter(typeReference), basicType.GenericParameterCount);
                    foreach (var gp in genericParameters)
                        typeReference.GenericParameters.Add(gp);
                } else
                {
                    // not instantiated
                    var analysisNetGenericType = basicType.GenericType;
                    Contract.Assert(analysisNetGenericType.GenericArguments.Count() == 0);

                    // cecil generic type with T0 ... Tn
                    var cecilGenericType = GenerateTypeReference(analysisNetGenericType);

                    List<TypeReference> arguments = new List<TypeReference>();
                    foreach (var arg in basicType.GenericArguments)
                    {
                        // Is it T_i ?
                        if (arg is IGenericParameterReference genericParameterRef &&
                            genericParameterRef.GenericContainer == basicType)
                            arguments.Add(cecilGenericType.GenericParameters.ElementAt(genericParameterRef.Index));
                        else
                            arguments.Add(GenerateTypeReference(arg));
                    }

                    typeReference = cecilGenericType.MakeGenericInstanceType(arguments.ToArray());
                }
            }

            return typeReference;
        }

        public TypeReference GenerateTypeReference(Model.Types.IGenericParameterReference genericParameterReference)
        {
            if (genericParameterReference.Kind == GenericParameterKind.Type)
            {
                // I generate a cecil reference to the generic container and from there I take the reference to the cecil generic parameter
                // I could not create directly the cecil generic parameter reference

                var cecilContainingType = this.GenerateTypeReference(genericParameterReference.GenericContainer as IBasicType);
                if (cecilContainingType is GenericInstanceType genericInstanceType)
                    return genericInstanceType.ElementType.GenericParameters.ElementAt(genericParameterReference.Index);
                return cecilContainingType.GenericParameters.ElementAt(genericParameterReference.Index);
            }
            else if (genericParameterReference.Kind == GenericParameterKind.Method)
            {
                Model.Types.IMethodReference analysisNetMethod = genericParameterReference.GenericContainer as IMethodReference;
                Mono.Cecil.MethodReference m = GenerateMethodReference(analysisNetMethod);
                return m.GenericParameters.ElementAt(genericParameterReference.Index);
            }
            else
                throw new NotImplementedException();
        }
 
        public TypeReference GenerateTypeReference(Model.Types.IType type)
        {
            if (type is IBasicType basicType)
                return GenerateTypeReference(basicType);

            if (type is IGenericParameterReference genericParameterReference)
                return GenerateTypeReference(genericParameterReference);

            if (type is Model.Types.FunctionPointerType functionPointerType)
            {
                throw new NotImplementedException();
            }

            if (type is Model.Types.PointerType pointerType)
            {
                // Mono.Cecil.PointerType is an unsafe reference
                return new Mono.Cecil.ByReferenceType(GenerateTypeReference(pointerType.TargetType));
            }

            if (type is Model.Types.ArrayType arrayType)
            {
                return TypeReferenceRocks.MakeArrayType(this.GenerateTypeReference(arrayType.ElementsType), (int)arrayType.Rank);
            }

            if (type is UnknownType unknownType)
            {
                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        private ModuleDefinition ModuleDefinitionForTypeDefinition(Model.Types.TypeDefinition typeDefinition)
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
