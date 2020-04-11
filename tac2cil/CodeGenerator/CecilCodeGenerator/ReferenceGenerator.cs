﻿using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    internal class ReferenceGenerator
    {

        // Maps a position in a generic argument list to a generic parameter
        // we use this map to prevent infinite recursion, specially with generic methods
        private class GenericParameterMap : Dictionary<int, Cecil.GenericParameter>
        {
        }

        public ReferenceGenerator(Context context)
        {
            Context = context;
            fieldsCache = context.ModelMapping.FieldsMap;
            typesCache = context.ModelMapping.TypesMap;
            methodsCache = context.ModelMapping.MethodsMap;

            genericParamsMap = new Dictionary<AnalysisNet.Types.IGenericReference, GenericParameterMap>();
        }

        public Context Context { get; }

        // cache only for performance
        private readonly IDictionary<AnalysisNet.Types.IFieldReference, Cecil.FieldReference> fieldsCache;
        private readonly IDictionary<AnalysisNet.Types.IType, Cecil.TypeReference> typesCache;
        private readonly IDictionary<AnalysisNet.Types.IMethodReference, Cecil.MethodReference> methodsCache;

        // used to prevent infinite recursion
        private readonly IDictionary<AnalysisNet.Types.IGenericReference, GenericParameterMap> genericParamsMap;

        private void MapGenericParameters(Cecil.IGenericParameterProvider cecilContainer, AnalysisNet.Types.IGenericReference analysisNetContainer)
        {
            GenericParameterMap map = new GenericParameterMap();

            for (int i = 0; i < cecilContainer.GenericParameters.Count; i++)
            {
                map[i] = cecilContainer.GenericParameters.ElementAt(i);
            }

            genericParamsMap[analysisNetContainer] = map;
        }

        private Cecil.GenericParameter GetGenericParameter(AnalysisNet.Types.IGenericParameterReference analysisNetGP)
        {
            if (genericParamsMap.TryGetValue(analysisNetGP.GenericContainer, out GenericParameterMap map))
            {
                return map[analysisNetGP.Index];
            }

            // populates map
            if (analysisNetGP.Kind == AnalysisNet.Types.GenericParameterKind.Method)
            {
                MethodReference(analysisNetGP.GenericContainer as AnalysisNet.Types.IMethodReference);
            }
            else
            {
                TypeReference(analysisNetGP.GenericContainer as AnalysisNet.Types.IBasicType);
            }

            map = genericParamsMap[analysisNetGP.GenericContainer];
            return map[analysisNetGP.Index];
        }

        public Cecil.FieldReference FieldReference(AnalysisNet.Types.IFieldReference fieldReference)
        {
            if (fieldsCache.TryGetValue(fieldReference, out Cecil.FieldReference cecilField))
            {
                return cecilField;
            }

            Cecil.TypeReference cecilType = TypeReference(fieldReference.Type);
            Cecil.TypeReference declaringType = TypeReference(fieldReference.ContainingType);

            cecilField = new Cecil.FieldReference(
                fieldReference.Name,
                cecilType,
                declaringType
            );

            fieldsCache[fieldReference] = cecilField;

            return cecilField;
        }

        public Cecil.MethodReference MethodReference(AnalysisNet.Types.IMethodReference methodReference)
        {
            if (methodsCache.TryGetValue(methodReference, out Cecil.MethodReference cecilMethodReference))
            {
                return cecilMethodReference;
            }

            Cecil.TypeReference dummyReturnType = Context.CurrentModule.TypeSystem.Void;
            Cecil.TypeReference declaringType = TypeReference(methodReference.ContainingType);

            string name = methodReference.Name;
            cecilMethodReference = new Cecil.MethodReference(name, dummyReturnType, declaringType)
            {
                HasThis = !methodReference.IsStatic
            };

            if (methodReference.GenericParameterCount > 0)
            {
                cecilMethodReference.CreateGenericParameters(methodReference.GenericParameterCount);
                MapGenericParameters(cecilMethodReference, methodReference);
                // should we add constraints?
                if (methodReference.GenericArguments.Count == 0)
                {
                    Cecil.GenericInstanceMethod instantiated = new Cecil.GenericInstanceMethod(cecilMethodReference);
                    instantiated.GenericArguments.AddRange(cecilMethodReference.GenericParameters);
                    cecilMethodReference = instantiated;
                }
                else
                {
                    IEnumerable<Cecil.TypeReference> arguments = methodReference.GenericArguments.Select(ga => TypeReference(ga));
                    Cecil.GenericInstanceMethod instantiated = new Cecil.GenericInstanceMethod(cecilMethodReference);
                    instantiated.GenericArguments.AddRange(arguments);
                    cecilMethodReference = instantiated;
                }
            }

            cecilMethodReference.ReturnType = TypeReference(methodReference.ReturnType);

            foreach (AnalysisNet.Types.IMethodParameterReference parameter in methodReference.Parameters)
            {
                Cecil.ParameterDefinition cecilParam = new Cecil.ParameterDefinition(TypeReference(parameter.Type));
                if (parameter.Kind == AnalysisNet.Types.MethodParameterKind.In)
                {
                    cecilParam.IsIn = true;
                }
                else if (parameter.Kind == AnalysisNet.Types.MethodParameterKind.Out)
                {
                    cecilParam.IsOut = true;
                }

                cecilMethodReference.Parameters.Add(cecilParam);
            }

            cecilMethodReference = Context.CurrentModule.ImportReference(cecilMethodReference);
            methodsCache[methodReference] = cecilMethodReference;

            return cecilMethodReference;
        }
        public Cecil.TypeReference TypeReference(AnalysisNet.Types.IType type)
        {
            if (type is AnalysisNet.Types.IBasicType basicType)
            {
                return TypeReference(basicType);
            }

            if (type is AnalysisNet.Types.IGenericParameterReference iGenericParam)
            {
                return TypeReference(iGenericParam);
            }

            if (type is AnalysisNet.Types.FunctionPointerType functionPointerType)
            {
                var funcPtr = new Cecil.FunctionPointerType()
                {
                    ReturnType = TypeReference(functionPointerType.ReturnType),
                    HasThis = !functionPointerType.IsStatic,

                };
                funcPtr.Parameters.AddRange(functionPointerType.Parameters.Select(p => new Cecil.ParameterDefinition(TypeReference(p.Type))));
                return funcPtr;
            }

            if (type is AnalysisNet.Types.PointerType pointerType)
            {
                // Mono.Cecil.PointerType is an unsafe reference
                return new Cecil.ByReferenceType(TypeReference(pointerType.TargetType));
            }

            if (type is AnalysisNet.Types.ArrayType arrayType)
            {
                return Cecil.Rocks.TypeReferenceRocks.MakeArrayType(TypeReference(arrayType.ElementsType), (int)arrayType.Rank);
            }

            if (type is AnalysisNet.Types.UnknownType unknownType)
            {
                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        private Cecil.ModuleDefinition ResolveModule(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            return Context.ModelMapping.AssembliesMap[typeDefinition.ContainingAssembly].MainModule;
        }

        private Cecil.TypeReference TypeReference(AnalysisNet.Types.IBasicType basicType)
        {
            if (typesCache.TryGetValue(basicType, out Cecil.TypeReference cecilTypeReference))
            {
                return cecilTypeReference;
            }

            Cecil.TypeReference platformType = TypeReferenceToPlatformType(basicType);
            if (platformType != null)
            {
                typesCache[basicType] = platformType;
                return platformType;
            }

            string nmspace = basicType.ContainingNamespace;
            string name = basicType.MetadataName();
            Cecil.ModuleDefinition module = ResolveModule(basicType);
            Cecil.IMetadataScope scope = module ?? ResolveScope(basicType);
            if (module == null && scope == null)
            {
                throw new NotImplementedException();
            }

            cecilTypeReference = new Cecil.TypeReference(nmspace, name, module, scope);
            if (basicType.TypeKind == AnalysisNet.Types.TypeKind.ValueType)
            {
                cecilTypeReference.IsValueType = true;
            }

            if (basicType.ContainingType != null)
            {
                cecilTypeReference.DeclaringType = TypeReference(basicType.ContainingType);
                cecilTypeReference.Namespace = string.Empty;
            }

            if (basicType.GenericParameterCount > 0)
            {
                Cecil.GenericInstanceType instantiated = null;
                // should we add constraints?
                cecilTypeReference.CreateGenericParameters(basicType.GenericParameterCount);
                MapGenericParameters(cecilTypeReference, basicType);
                // call it before instantiating it
                cecilTypeReference = ImportTypeReference(cecilTypeReference);

                if (basicType.GenericArguments.Count == 0)
                {
                    instantiated = cecilTypeReference.MakeGenericInstanceType(cecilTypeReference.GenericParameters.ToArray());
                }
                else
                {
                    Cecil.TypeReference[] arguments = basicType.GenericArguments.Select(ga => TypeReference(ga)).ToArray();
                    instantiated = cecilTypeReference.MakeGenericInstanceType(arguments);
                }

                cecilTypeReference = instantiated;
            }
            else
            {
                cecilTypeReference = ImportTypeReference(cecilTypeReference);
            }

            typesCache[basicType] = cecilTypeReference;
            return cecilTypeReference;
        }

        private Cecil.ModuleDefinition ResolveModule(AnalysisNet.Types.IBasicType basicType)
        {
            if (basicType.ResolvedType != null)
            {
                return ResolveModule(basicType.ResolvedType);
            }

            return null;
        }

        private Cecil.IMetadataScope ResolveScope(AnalysisNet.Types.IBasicType basicType)
        {
            if (basicType.ContainingAssembly.Name.Equals("mscorlib"))
            {
                return Context.CurrentModule.TypeSystem.CoreLibrary;
            }

            if (basicType.ContainingAssembly.Name.Equals("System.Core"))
            {
                //var enumerable = Context.CurrentModule.ImportReference(typeof(Enumerable));
                //var r = enumerable.Module.AssemblyReferences.Where(assembly => assembly.Name.Equals("System.Core")).First();
                //return r;
                Cecil.AssemblyNameReference a = new Cecil.AssemblyNameReference(basicType.ContainingAssembly.Name, new Version(4, 0, 0, 0))
                {
                    PublicKeyToken = new byte[8] { 183, 122, 92, 86, 25, 52, 224, 137 }
                };
                return a;
            }

            if (basicType.ContainingAssembly.Name.Equals("System"))
            {
                Cecil.AssemblyNameReference a = new Cecil.AssemblyNameReference(basicType.ContainingAssembly.Name, new Version(4, 0, 0, 0))
                {
                    PublicKeyToken = new byte[8] { 183, 122, 92, 86, 25, 52, 224, 137 }
                };
                return a;
            }

            return null;
        }

        private Cecil.TypeReference ImportTypeReference(Cecil.TypeReference typeReference)
        {
            if (typeReference.Module == null || typeReference.Module != Context.CurrentModule)
            {
                typeReference = Context.CurrentModule.ImportReference(typeReference);
            }

            return typeReference;
        }


        private Cecil.TypeReference TypeReferenceToPlatformType(AnalysisNet.Types.IBasicType basicType)
        {
            if (basicType is AnalysisNet.Types.PlatformType platformType)
            {
                var specificImplementation = platformType.ToImplementation(Context.CurrentModule.TypeSystem.CoreLibrary.Name);
                specificImplementation.Resolve(Context.Host);
                return TypeReference(specificImplementation);
            }

            return null;
        }

        private Cecil.TypeReference TypeReference(AnalysisNet.Types.IGenericParameterReference genericParameter)
        {
            if (genericParameter.Kind == AnalysisNet.Types.GenericParameterKind.Type)
            {
                if (typesCache.TryGetValue(genericParameter, out Cecil.TypeReference cecilParam))
                {
                    return cecilParam;
                }

                cecilParam = GetGenericParameter(genericParameter);
                typesCache[genericParameter] = cecilParam;
                return cecilParam;
            }
            else if (genericParameter.Kind == AnalysisNet.Types.GenericParameterKind.Method)
            {
                if (typesCache.TryGetValue(genericParameter, out Cecil.TypeReference cecilParam))
                {
                    return cecilParam;
                }

                cecilParam = GetGenericParameter(genericParameter);
                typesCache[genericParameter] = cecilParam;
                return cecilParam;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
