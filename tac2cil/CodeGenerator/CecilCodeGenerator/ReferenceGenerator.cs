using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;

namespace CodeGenerator.CecilCodeGenerator
{
    class ReferenceGenerator
    {
        public ReferenceGenerator(Context context)
        {
            this.Context = context;
        }

        public Context Context { get; }

        public Cecil.FieldReference FieldReference(AnalysisNet.Types.IFieldReference fieldReference)
        {
            var cache = Context.ModelMapping.FieldsMap;
            if (cache.ContainsKey(fieldReference))
                return cache[fieldReference];

            Cecil.TypeReference cecilType = TypeReference(fieldReference.Type);
            Cecil.TypeReference declaringType = TypeReference(fieldReference.ContainingType);

            Cecil.FieldReference cecilField = new Cecil.FieldReference(
                fieldReference.Name,
                cecilType,
                declaringType
            );

            cache[fieldReference] = cecilField;

            return cecilField;
        }

        public Cecil.MethodReference MethodReference(AnalysisNet.Types.IMethodReference methodReference)
        {
            var cache = Context.ModelMapping.MethodsMap;
            if (cache.ContainsKey(methodReference))
                return cache[methodReference];

            Cecil.TypeReference dummyReturnType = Context.CurrentModule.TypeSystem.Void;
            Cecil.TypeReference declaringType = TypeReference(methodReference.ContainingType);

            string name = methodReference.Name;
            Cecil.MethodReference cecilMethodReference = new Cecil.MethodReference(name, dummyReturnType, declaringType);
            cecilMethodReference.HasThis = !methodReference.IsStatic;

            if (methodReference.GenericParameterCount > 0)
            {
                cecilMethodReference.CreateGenericParameters(methodReference.GenericParameterCount);
                // should we add constraints?
                if (methodReference.GenericArguments.Count == 0)
                {
                    var instantiated = new Cecil.GenericInstanceMethod(cecilMethodReference);
                    instantiated.GenericArguments.AddRange(cecilMethodReference.GenericParameters);
                    cecilMethodReference = instantiated;
                }
                else
                {
                    var arguments = methodReference.GenericArguments.Select(ga => TypeReference(ga));
                    var instantiated = new Cecil.GenericInstanceMethod(cecilMethodReference);
                    instantiated.GenericArguments.AddRange(arguments);
                    cecilMethodReference = instantiated;
                }
            }

            cache[methodReference] = cecilMethodReference;
            cecilMethodReference.ReturnType = TypeReference(methodReference.ReturnType);

            foreach (var parameter in methodReference.Parameters)
            {
                var cecilParam = new Cecil.ParameterDefinition(TypeReference(parameter.Type));
                cecilMethodReference.Parameters.Add(cecilParam);
            }

            cecilMethodReference = Context.CurrentModule.ImportReference(cecilMethodReference);
            return cecilMethodReference;
        }
        public Cecil.TypeReference TypeReference(AnalysisNet.Types.IType type)
        {
            if (type is AnalysisNet.Types.IBasicType basicType)
                return TypeReference(basicType);

            if (type is AnalysisNet.Types.IGenericParameterReference iGenericParam)
                return TypeReference(iGenericParam);

            if (type is AnalysisNet.Types.FunctionPointerType functionPointerType)
            {
                throw new NotImplementedException();
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
            if (Context.ModelMapping.TypesMap.ContainsKey(basicType))
                return Context.ModelMapping.TypesMap[basicType];

            Cecil.TypeReference platformType = TypeReferenceToPlatformType(basicType);
            if (platformType != null)
                return platformType;

            string nmspace = basicType.ContainingNamespace;
            string name = basicType.MetadataName();
            Cecil.ModuleDefinition module = ResolveModule(basicType);
            Cecil.IMetadataScope scope = module ?? ResolveScope(basicType);
            if (module == null && scope == null)
                throw new NotImplementedException();

            Cecil.TypeReference cecilTypeReference = new Cecil.TypeReference(nmspace, name, module, scope);

            if (basicType.GenericParameterCount > 0)
            {
                Cecil.GenericInstanceType instantiated = null;
                // should we add constraints?
                cecilTypeReference.CreateGenericParameters(basicType.GenericParameterCount);

                // call it before instantiating it
                cecilTypeReference = ImportTypeReference(cecilTypeReference);

                if (basicType.GenericArguments.Count == 0)
                {
                    instantiated = cecilTypeReference.MakeGenericInstanceType(cecilTypeReference.GenericParameters.ToArray());
                }
                else
                {
                    var arguments = basicType.GenericArguments.Select(ga => TypeReference(ga)).ToArray();
                    instantiated = cecilTypeReference.MakeGenericInstanceType(arguments);
                }

                // should we cache it before instantiating when there are arguments available?
                Context.ModelMapping.TypesMap[basicType] = instantiated;

                cecilTypeReference = instantiated;
            }
            else
            {
                cecilTypeReference = ImportTypeReference(cecilTypeReference);
            }

            if (basicType.ContainingType != null)
                cecilTypeReference.DeclaringType = TypeReference(basicType.ContainingType);


            return cecilTypeReference;
        }

        private Cecil.ModuleDefinition ResolveModule(AnalysisNet.Types.IBasicType basicType)
        {
            if (basicType.ResolvedType != null)
                return ResolveModule(basicType.ResolvedType);

            return null;
        }

        private Cecil.IMetadataScope ResolveScope(AnalysisNet.Types.IBasicType basicType)
        {
            if (basicType.ContainingAssembly.Name.Equals("mscorlib"))
                return Context.CurrentModule.TypeSystem.CoreLibrary;

            return null;
        }

        private Cecil.TypeReference ImportTypeReference(Cecil.TypeReference typeReference)
        {
            if (typeReference.Module == null || typeReference.Module != Context.CurrentModule)
                typeReference = Context.CurrentModule.ImportReference(typeReference);

            return typeReference;
        }


        private Cecil.TypeReference TypeReferenceToPlatformType(AnalysisNet.Types.IBasicType basicType)
        {
            if (basicType.Equals(Model.Types.PlatformTypes.Object))
                return Context.CurrentModule.TypeSystem.Object;

            if (basicType.Equals(Model.Types.PlatformTypes.Void))
                return Context.CurrentModule.TypeSystem.Void;

            if (basicType.Equals(Model.Types.PlatformTypes.Int32))
                return Context.CurrentModule.TypeSystem.Int32;

            if (basicType.Equals(Model.Types.PlatformTypes.String))
                return Context.CurrentModule.TypeSystem.String;

            if (basicType.Equals(Model.Types.PlatformTypes.Boolean))
                return Context.CurrentModule.TypeSystem.Boolean;

            if (basicType.Equals(Model.Types.PlatformTypes.Single))
                return Context.CurrentModule.TypeSystem.Single;

            return null;
        }

        private Cecil.TypeReference TypeReference(AnalysisNet.Types.IGenericParameterReference genericParameter)
        {
            if (genericParameter.Kind == AnalysisNet.Types.GenericParameterKind.Type)
            {
                var cache = Context.ModelMapping.TypesMap;
                if (cache.ContainsKey(genericParameter))
                    return cache[genericParameter];

                AnalysisNet.Types.IBasicType container = genericParameter.GenericContainer as AnalysisNet.Types.IBasicType;
                
                // genericParameter can be !0 and the generic container List<int>, without the fallback to the ElementType
                // we would return int which is wrong
                
                Cecil.GenericInstanceType cecilContainerReference = (cache.ContainsKey(container) ? cache[container] : TypeReference(container)) as Cecil.GenericInstanceType;
                Cecil.TypeReference cecilParam = cecilContainerReference.GenericArguments.ElementAt(genericParameter.Index);
                if (!(cecilParam is Cecil.GenericParameter))
                    cecilParam = cecilContainerReference.ElementType.GenericParameters.ElementAt(genericParameter.Index);
                cache[genericParameter] = cecilParam;
                return cecilParam;
            }
            else if (genericParameter.Kind == AnalysisNet.Types.GenericParameterKind.Method)
            {
                var typeCache = Context.ModelMapping.TypesMap;
                if (typeCache.ContainsKey(genericParameter))
                    return typeCache[genericParameter];

                var methodCache = Context.ModelMapping.MethodsMap;

                AnalysisNet.Types.IMethodReference container = genericParameter.GenericContainer as AnalysisNet.Types.IMethodReference;
                Cecil.GenericInstanceMethod cecilContainerReference = (methodCache.ContainsKey(container) ? methodCache[container] : MethodReference(container)) as Cecil.GenericInstanceMethod;
                Cecil.TypeReference cecilParam = cecilContainerReference.GenericArguments.ElementAt(genericParameter.Index);
                if (!(cecilParam is Cecil.GenericParameter))
                    cecilParam = cecilContainerReference.GetElementMethod().GenericParameters.ElementAt(genericParameter.Index);
                typeCache[genericParameter] = cecilParam;
                return cecilParam;
            }
            else
                throw new NotImplementedException();
        }
    }
}
