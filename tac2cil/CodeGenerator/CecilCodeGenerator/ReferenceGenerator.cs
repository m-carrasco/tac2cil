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

        private bool IsInstantiatedOrGenericParameter(AnalysisNet.Types.IFieldReference fieldReference)
        {
            if (fieldReference.Type is AnalysisNet.Types.IGenericParameterReference)
                return true;
            return false;
        }

        public Cecil.FieldReference FieldReference(AnalysisNet.Types.IFieldReference fieldReference)
        {
            Cecil.TypeReference cecilType = null;

            // handle field type for generic parameters
            if (fieldReference.Type is AnalysisNet.Types.GenericParameter genericParameter)
            {
                // cecil counterpart of the generic container has the cecil generic parameter
                AnalysisNet.Types.TypeDefinition genericContainerDef
                    = genericParameter.GenericContainer as AnalysisNet.Types.TypeDefinition;

                var cecilGenericParam = Context.DefinitionMapping.TypesMap[genericContainerDef].GenericParameters.ElementAt(genericParameter.Index);
                cecilType = cecilGenericParam;
            } else if (fieldReference.Type is AnalysisNet.Types.GenericParameterReference genericReference)
            {
                var cecilGenericContainer = TypeReference(genericReference.GenericContainer as AnalysisNet.Types.IBasicType);

                if (cecilGenericContainer.HasGenericParameters)
                    cecilType = cecilGenericContainer.GenericParameters.ElementAt(genericReference.Index);
                else
                    cecilType = cecilGenericContainer.GetElementType().GenericParameters.ElementAt(genericReference.Index);
            }
            else
            {
                cecilType = TypeReference(fieldReference.Type);
            }

            var declaringType = TypeReference(fieldReference.ContainingType);

            Cecil.FieldReference cecilField = new Cecil.FieldReference(
                fieldReference.Name,
                cecilType,
                declaringType
            );

            return cecilField;
        }

        public Cecil.MethodReference MethodReference(AnalysisNet.Types.IMethodReference iMethodReference)
        {
            if (iMethodReference is AnalysisNet.Types.MethodDefinition methodDefinition)
                return MethodReference(methodDefinition);

            if (iMethodReference is AnalysisNet.Types.MethodReference methodReference)
                return MethodReference(methodReference);

            throw new NotImplementedException();
        }

        private Cecil.MethodReference MethodReference(AnalysisNet.Types.MethodReference methodReference)
        {
            if (methodReference.ResolvedMethod != null)
            {
                if (methodReference.GenericParameterCount == 0) // non-generic
                    return CreateExternalMethodReference(methodReference);
  
                if (methodReference.GenericArguments.Count == 0) // uninstantiated
                    return MethodReference(methodReference.ResolvedMethod);

                var uninstantiated = MethodReference(methodReference.ResolvedMethod);
                var instantiatedMethod = new Cecil.GenericInstanceMethod(uninstantiated);
                instantiatedMethod.GenericArguments.AddRange(methodReference.GenericArguments.Select(ga => TypeReference(ga)).ToArray());
                return instantiatedMethod;
            }
            else
            {
                return CreateExternalMethodReference(methodReference);
            }
        }

        private Cecil.MethodReference MethodReference(AnalysisNet.Types.MethodDefinition methodDefinition)
        {
            return CreateExternalMethodReference(methodDefinition);
        }

        public Cecil.TypeReference TypeReference(AnalysisNet.Types.IType type)
        {
            if (type is AnalysisNet.Types.IBasicType iBasicType)
            {
                if (iBasicType is AnalysisNet.Types.TypeDefinition typeDefinition)
                    return TypeReference(typeDefinition);

                if (iBasicType is AnalysisNet.Types.BasicType basicType)
                    return TypeReference(basicType);

                throw new NotImplementedException();
            }

            if (type is AnalysisNet.Types.IGenericParameterReference)
            {
                if (type is AnalysisNet.Types.GenericParameter genericParameter)
                    return TypeReference(genericParameter);

                if (type is AnalysisNet.Types.GenericParameterReference genericParameterReference)
                    return TypeReference(genericParameterReference);
            }

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
        private Cecil.MethodReference CreateExternalMethodReference(AnalysisNet.Types.IMethodReference method)
        {
            var containingType = TypeReference(method.ContainingType);

            var methodReference = new Mono.Cecil.MethodReference(method.Name,
                Context.CurrentModule.TypeSystem.Void,
                containingType);

            methodReference.GenericParameters.AddRange(Enumerable.Repeat(new Mono.Cecil.GenericParameter(methodReference), method.GenericParameterCount));

            Cecil.TypeReference returnType;
            if (method.ReturnType is AnalysisNet.Types.IGenericParameterReference genericParameterReference && genericParameterReference.GenericContainer == method)
                returnType = methodReference.GenericParameters.ElementAt(genericParameterReference.Index);
            else
                returnType = TypeReference(method.ReturnType);

            methodReference.ReturnType = returnType;

            foreach (var param in method.Parameters)
            {
                Cecil.TypeReference paramType;

                if (param.Type is AnalysisNet.Types.IGenericParameterReference genericParameterReferenceParam && genericParameterReferenceParam.GenericContainer == method)
                    paramType = methodReference.GenericParameters.ElementAt(genericParameterReferenceParam.Index);
                else
                    paramType = this.TypeReference(param.Type);

                methodReference.Parameters.Add(new Cecil.ParameterDefinition(paramType));
            }

            if (!method.IsStatic)
                methodReference.HasThis = true;

            if (method.GenericArguments.Count > 0)
            {
                var genericInstance = new Cecil.GenericInstanceMethod(methodReference);
                foreach (var arg in method.GenericArguments)
                {
                    if (arg is AnalysisNet.Types.IGenericParameterReference genericParameterReferenceParam && genericParameterReferenceParam.GenericContainer == method)
                        genericInstance.GenericArguments.Add(methodReference.GenericParameters.ElementAt(genericParameterReferenceParam.Index));
                    else
                        genericInstance.GenericArguments.Add(TypeReference(arg));
                }

                methodReference = genericInstance;
            }

            if (method.GenericParameterCount > 0 && method.ResolvedMethod != null)
            {
                AnalysisNet.Types.MethodDefinition analysisNetDef = method.ResolvedMethod;
                foreach (var gp in methodReference.GenericParameters)
                {
                    var constraints = analysisNetDef.GenericParameters.ElementAt(gp.Position)
                        .Constraints.Select(constraint => new Cecil.GenericParameterConstraint(TypeReference(constraint)));
                    gp.Constraints.AddRange(constraints);
                }
            }

            methodReference = Context.CurrentModule.ImportReference(methodReference);
            return methodReference;
        }

        private Cecil.TypeReference TypeReference(AnalysisNet.Types.TypeDefinition typeDefinition)
        {
            var cecilDefinedType = Context.DefinitionMapping.TypesMap[typeDefinition];

            if (typeDefinition.GenericParameters.Count > 0)
            {
                var genericCecil = new Cecil.TypeReference(cecilDefinedType.Namespace, cecilDefinedType.Name, cecilDefinedType.Module, cecilDefinedType.Scope);
                foreach (var cecilGP in cecilDefinedType.GenericParameters)
                {
                    var newGP = new Cecil.GenericParameter(genericCecil);
                    foreach (var cecilConstraint in cecilGP.Constraints)
                    {
                        var newConstraint = new Cecil.GenericParameterConstraint(cecilConstraint.ConstraintType);
                        newGP.Constraints.Add(newConstraint);
                    }
                    genericCecil.GenericParameters.Add(newGP);
                }
                
                return genericCecil;
            }

            return cecilDefinedType;
        }

        // que pasa si basictype es un type generico, no instanciado.
        // Si devuelvo directo el type definition de cecil, no tiene parametros
        private Cecil.TypeReference TypeReference(AnalysisNet.Types.BasicType basicType)
        {
            Cecil.TypeReference platformType = TypeReferenceToPlatformType(basicType);
            if (platformType != null)
                return platformType;

            // non-generic and defined
            if (basicType.GenericParameterCount == 0 && basicType.ResolvedType != null)
                return TypeReference(basicType.ResolvedType);

            // generic and defined
            if (basicType.GenericParameterCount > 0 && basicType.ResolvedType != null)
            {
                if (basicType.GenericArguments.Count == 0)
                    return TypeReference(basicType.ResolvedType);
                else
                    return TypeReference(basicType.ResolvedType).MakeGenericInstanceType(basicType.GenericArguments.Select(ga => TypeReference(ga)).ToArray());
            }

            Cecil.ModuleDefinition moduleDefinition = null;
            Cecil.IMetadataScope metadataScope = null;
            SetModuleAndMetadata(basicType, ref moduleDefinition, ref metadataScope);

            Cecil.TypeReference typeReference = new Cecil.TypeReference(basicType.ContainingNamespace, basicType.MetadataName(), moduleDefinition, metadataScope); ;

            // if there is no module (so it is an assembly reference) or the module is not the current one, import the reference.
            if (moduleDefinition == null || moduleDefinition != Context.CurrentModule)
                typeReference = Context.CurrentModule.ImportReference(typeReference);

            CreateGenericParameters(basicType, ref typeReference);

            return typeReference;
        }
        private void SetModuleAndMetadata(AnalysisNet.Types.IBasicType basicType, ref Cecil.ModuleDefinition moduleDefinition, ref Cecil.IMetadataScope metadataScope)
        {
            // for now we only handle it for the mscorlib

            if (basicType.ContainingAssembly.Name.Equals("mscorlib"))
            {
                metadataScope = Context.CurrentModule.TypeSystem.CoreLibrary;
            }
            else
                throw new NotImplementedException();
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

        private void CreateGenericParameters(AnalysisNet.Types.IBasicType basicType, ref Cecil.TypeReference typeReference)
        {
            if (basicType.GenericParameterCount > 0)
            {
                if (basicType.GenericArguments.Count == 0)
                {
                    // create generic parameters
                    var genericParameters = Enumerable.Repeat(new Mono.Cecil.GenericParameter(typeReference), basicType.GenericParameterCount);
                    typeReference.GenericParameters.AddRange(genericParameters);
                }
                else
                {
                    // not instantiated
                    var analysisNetGenericType = basicType.GenericType;
                    Contract.Assert(analysisNetGenericType.GenericArguments.Count() == 0);

                    // cecil generic type with T0 ... Tn
                    var cecilGenericType = TypeReference(analysisNetGenericType);

                    List<Cecil.TypeReference> arguments = new List<Cecil.TypeReference>();
                    foreach (var arg in basicType.GenericArguments)
                    {
                        // Is it T_i ?
                        if (arg is AnalysisNet.Types.IGenericParameterReference genericParameterRef &&
                            genericParameterRef.GenericContainer == basicType)
                            arguments.Add(cecilGenericType.GenericParameters.ElementAt(genericParameterRef.Index));
                        else
                            arguments.Add(TypeReference(arg));
                    }

                    typeReference = cecilGenericType.MakeGenericInstanceType(arguments.ToArray());
                }

                if (basicType.ResolvedType != null)
                {
                    AnalysisNet.Types.TypeDefinition analysisNetDef = basicType.ResolvedType;

                    foreach (var gp in typeReference.GenericParameters)
                    {
                        var constraints = analysisNetDef.GenericParameters.ElementAt(gp.Position)
                            .Constraints.Select(constraint => new Cecil.GenericParameterConstraint(TypeReference(constraint)));
                        gp.Constraints.AddRange(constraints);
                    }
                }
            }
        }

        private Cecil.TypeReference TypeReference(AnalysisNet.Types.GenericParameter genericParameter)
        {
            if (genericParameter.GenericContainer is AnalysisNet.Types.TypeDefinition typeDefinition)
            {
                var cecilTypeDefinition = Context.DefinitionMapping.TypesMap[typeDefinition];
                var cecilParameter = cecilTypeDefinition.GenericParameters.ElementAt(genericParameter.Index);
                return cecilParameter;
            }
            else if (genericParameter.GenericContainer is AnalysisNet.Types.MethodDefinition methodDefinition)
            {
                var cecilTypeDefinition = Context.DefinitionMapping.MethodsMap[methodDefinition];
                var cecilParameter = cecilTypeDefinition.GenericParameters.ElementAt(genericParameter.Index);
                return cecilParameter;
            }
            else
                throw new NotImplementedException();
        }

        private Cecil.TypeReference TypeReference(AnalysisNet.Types.GenericParameterReference genericParameterReference)
        {
            if (genericParameterReference.GenericContainer is AnalysisNet.Types.MethodReference methodReference)
            {
                var cecilMethodRef = MethodReference(methodReference.GenericMethod);
                return cecilMethodRef.GenericParameters.ElementAt(genericParameterReference.Index);
            }
            else if (genericParameterReference.GenericContainer is AnalysisNet.Types.BasicType basicType)
            {
                var cecilTypeRef = TypeReference(basicType.GenericType);
                return cecilTypeRef.GenericParameters.ElementAt(genericParameterReference.Index);
            }
            else
                throw new NotImplementedException();
        }
    }
}
