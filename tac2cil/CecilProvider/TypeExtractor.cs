using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using AnalysisNet = Model;
using Cecil = Mono.Cecil;

namespace CecilProvider
{
    internal class TypeExtractor
    {
        private class GenericParameterExtractor
        {
            public GenericParameterExtractor(TypeExtractor typeExtractor)
            {
                this.typeExtractor = typeExtractor;
                genericParamsMap = new Dictionary<Cecil.IGenericParameterProvider, GenericParameterMap>();
            }

            private class GenericParameterMap : Dictionary<int, AnalysisNet.Types.IGenericParameterReference> { }
            private readonly IDictionary<Cecil.IGenericParameterProvider, GenericParameterMap> genericParamsMap;
            private readonly TypeExtractor typeExtractor;

            private AnalysisNet.Types.GenericParameterKind GetKind(Cecil.GenericParameter cecilContainer)
            {
                if (cecilContainer.Type == Cecil.GenericParameterType.Type)
                {
                    return AnalysisNet.Types.GenericParameterKind.Type;
                }
                else if (cecilContainer.Type == Cecil.GenericParameterType.Method)
                {
                    return AnalysisNet.Types.GenericParameterKind.Method;
                }

                throw new NotImplementedException();
            }
            public void MapGenericParameters(Cecil.IGenericParameterProvider cecilContainer, AnalysisNet.Types.IGenericReference analysisNetContainer)
            {
                GenericParameterMap map = new GenericParameterMap();

                for (int i = 0; i < cecilContainer.GenericParameters.Count; i++)
                {
                    Cecil.GenericParameter cecilParam = cecilContainer.GenericParameters.ElementAt(i);
                    AnalysisNet.Types.GenericParameterReference analysisNetParam = new AnalysisNet.Types.GenericParameterReference(GetKind(cecilParam), (ushort)cecilParam.Position)
                    {
                        GenericContainer = analysisNetContainer
                    };
                    map[i] = analysisNetParam;
                }

                genericParamsMap[cecilContainer] = map;

                //throw new NotImplementedException();
            }

            public AnalysisNet.Types.IGenericParameterReference GetGenericParameter(Cecil.GenericParameter cecilParameter)
            {
                Cecil.IGenericParameterProvider owner = cecilParameter.Owner;
                if (genericParamsMap.TryGetValue(owner, out GenericParameterMap map))
                {
                    return map[cecilParameter.Position];
                }

                // populates map
                if (cecilParameter.Type == Cecil.GenericParameterType.Method)
                {
                    typeExtractor.ExtractMethod(owner as Cecil.MethodReference);
                }
                else
                {
                    // check my assumption
                    if ((owner is Cecil.GenericInstanceType))
                    {
                        throw new Exception();
                    }

                    typeExtractor.ExtractType(owner as Cecil.TypeReference);
                }

                map = genericParamsMap[owner];
                return map[cecilParameter.Position];
            }
        }
        private readonly AnalysisNet.Host host;
        private readonly MemoryCache performanceCache;
        // usted to prevent infinite recursion while creating generic parameter references
        private readonly GenericParameterExtractor genericParameterExtractor;

        public TypeExtractor(AnalysisNet.Host host)
        {
            this.host = host;
            genericParameterExtractor = new GenericParameterExtractor(this);

            IOptions<MemoryCacheOptions> optionsAccessor = new MemoryCacheOptions();
            performanceCache = new MemoryCache(optionsAccessor);
        }

        private bool IsDelegate(Cecil.TypeDefinition cecilType)
        {
            Cecil.TypeReference baseType = cecilType.BaseType;

            if (baseType == null)
            {
                return false;
            }

            Cecil.IMetadataScope coreLibrary = cecilType.Module.TypeSystem.CoreLibrary;

            return coreLibrary.Equals(baseType.Scope) && baseType.Namespace == "System" && baseType.Name == "MulticastDelegate";
        }

        public AnalysisNet.Types.TypeDefinition ExtractTypeDefinition(Cecil.TypeDefinition cecilType)
        {
            AnalysisNet.Types.TypeDefinition result;

            // the order matters
            // an enum can be a value type
            if (cecilType.IsEnum)
            {
                result = ExtractEnum(cecilType);
            }
            else if (cecilType.IsValueType)
            {
                result = ExtractClass(cecilType, AnalysisNet.Types.TypeKind.ValueType, AnalysisNet.Types.TypeDefinitionKind.Struct);
            }
            else if (cecilType.IsClass)
            {
                // includes delegates!
                AnalysisNet.Types.TypeDefinitionKind kind = IsDelegate(cecilType) ? AnalysisNet.Types.TypeDefinitionKind.Delegate : AnalysisNet.Types.TypeDefinitionKind.Class;
                result = ExtractClass(cecilType, AnalysisNet.Types.TypeKind.ReferenceType, kind);
            }
            else if (cecilType.IsInterface)
            {
                result = ExtractInterface(cecilType);
            }
            else
            {
                throw new NotImplementedException();
            }

            return result;
        }
        private AnalysisNet.Types.TypeDefinition ExtractEnum(Cecil.TypeDefinition typedef)
        {
            string name = typedef.Name;
            AnalysisNet.Types.TypeDefinition type = new AnalysisNet.Types.TypeDefinition(name, AnalysisNet.Types.TypeKind.ValueType, AnalysisNet.Types.TypeDefinitionKind.Enum)
            {
                Base = ExtractType(typedef.BaseType) as AnalysisNet.Types.IBasicType,
                IsAbstract = typedef.IsAbstract,
                IsSealed = typedef.IsSealed
            };
            Cecil.FieldDefinition valueField = typedef.Fields.Single(f => f.Name == "value__");
            type.UnderlayingType = ExtractType(valueField.FieldType) as AnalysisNet.Types.IBasicType;

            ExtractCustomAttributes(type.Attributes, typedef.CustomAttributes);
            ExtractConstants(type, type.Fields, typedef.Fields);

            return type;
        }

        private void ExtractConstants(AnalysisNet.Types.TypeDefinition containingType, IList<AnalysisNet.Types.FieldDefinition> dest, IEnumerable<Cecil.FieldDefinition> source)
        {
            source = source.Skip(1);

            foreach (Cecil.FieldDefinition constdef in source)
            {
                if (!constdef.HasConstant)
                {
                    continue;
                }

                string name = constdef.Name;
                AnalysisNet.Types.FieldDefinition constant = new AnalysisNet.Types.FieldDefinition(name, containingType)
                {
                    Value = new AnalysisNet.ThreeAddressCode.Values.Constant(constdef.Constant)
                    {
                        Type = containingType.UnderlayingType
                    }
                };

                constant.ContainingType = containingType;
                dest.Add(constant);
            }
        }

        // call after every method definition in analysisNetType is extracted
        private void ExtractPropertyDefinitions(AnalysisNet.Types.TypeDefinition analysisNetType, Cecil.TypeDefinition cecilType)
        {
            foreach (Cecil.PropertyDefinition cecilProperty in cecilType.Properties)
            {
                AnalysisNet.Types.PropertyDefinition ourProp = new AnalysisNet.Types.PropertyDefinition(cecilProperty.Name, ExtractType(cecilProperty.PropertyType))
                {
                    ContainingType = analysisNetType
                };

                if (cecilProperty.GetMethod != null)
                {
                    // It is a reference but we need the definition.
                    // It is not safe to call ResolvedMethod at this point, the model is incomplete.
                    AnalysisNet.Types.IMethodReference getterRef = ExtractMethod(cecilProperty.GetMethod);
                    ourProp.Getter = analysisNetType.Methods.Where(methodDef => methodDef.MatchSignature(getterRef)).First();
                }
                if (cecilProperty.SetMethod != null)
                {
                    // It is a reference but we need the definition.
                    // It is not safe to call ResolvedMethod at this point, the model is incomplete.
                    AnalysisNet.Types.IMethodReference setterRef = ExtractMethod(cecilProperty.SetMethod);
                    ourProp.Setter = analysisNetType.Methods.Where(methodDef => methodDef.MatchSignature(setterRef)).First();
                }

                ExtractCustomAttributes(ourProp.Attributes, cecilProperty.CustomAttributes);
                analysisNetType.PropertyDefinitions.Add(ourProp);
            }
        }
        private AnalysisNet.Types.TypeDefinition ExtractInterface(Cecil.TypeDefinition cecilType)
        {
            string name = UnmangleName(cecilType);
            AnalysisNet.Types.TypeDefinition type = new AnalysisNet.Types.TypeDefinition(name, AnalysisNet.Types.TypeKind.ReferenceType, AnalysisNet.Types.TypeDefinitionKind.Interface)
            {
                IsAbstract = cecilType.IsAbstract,
                IsSealed = cecilType.IsSealed
            };

            ExtractCustomAttributes(type.Attributes, cecilType.CustomAttributes);
            ExtractGenericTypeParameters(type, cecilType);
            ExtractInterfaces(type.Interfaces, cecilType.Interfaces);
            ExtractMethods(type, type.Methods, cecilType.Methods);
            ExtractPropertyDefinitions(type, cecilType);
            return type;
        }

        private AnalysisNet.Types.TypeDefinition ExtractClass(Cecil.TypeDefinition cecilType, AnalysisNet.Types.TypeKind typeKind, AnalysisNet.Types.TypeDefinitionKind typeDefinitionKind)
        {
            string name = UnmangleName(cecilType);
            AnalysisNet.Types.TypeDefinition type = new AnalysisNet.Types.TypeDefinition(name, typeKind, typeDefinitionKind);
            Cecil.TypeReference basedef = cecilType.BaseType;

            type.IsAbstract = cecilType.IsAbstract;
            type.IsSealed = cecilType.IsSealed;

            if (basedef != null)
            {
                type.Base = ExtractType(basedef) as AnalysisNet.Types.IBasicType;
            }

            ExtractCustomAttributes(type.Attributes, cecilType.CustomAttributes);
            ExtractGenericTypeParameters(type, cecilType);
            ExtractInterfaces(type.Interfaces, cecilType.Interfaces);
            ExtractFields(type, type.Fields, cecilType.Fields);
            ExtractMethods(type, type.Methods, cecilType.Methods);
            ExtractPropertyDefinitions(type, cecilType);
            ExtractExplicitMethodOverrides(type, cecilType);
            ExtractLayoutInformation(type, cecilType);

            return type;
        }

        private void ExtractLayoutInformation(AnalysisNet.Types.TypeDefinition type, Cecil.TypeDefinition typeDefinition)
        {
            AnalysisNet.Types.LayoutKind kind;
            if (typeDefinition.IsAutoLayout)
            {
                kind = AnalysisNet.Types.LayoutKind.AutoLayout;
            }
            else if (typeDefinition.IsExplicitLayout)
            {
                kind = AnalysisNet.Types.LayoutKind.ExplicitLayout;
            }
            else if (typeDefinition.IsSequentialLayout)
            {
                kind = AnalysisNet.Types.LayoutKind.SequentialLayout;
            }
            else
            {
                throw new NotImplementedException();
            }

            Model.Types.LayoutInformation layoutInformation = new AnalysisNet.Types.LayoutInformation(kind)
            {
                ClassSize = typeDefinition.ClassSize,
                PackingSize = typeDefinition.PackingSize
            };

            type.LayoutInformation = layoutInformation;
        }

        private void ExtractExplicitMethodOverrides(AnalysisNet.Types.TypeDefinition type, Cecil.TypeDefinition typeDefinition)
        {
            foreach (Cecil.MethodDefinition method in typeDefinition.Methods)
            {
                AnalysisNet.Types.IMethodReference implementingMethod = ExtractMethod(method);
                Mono.Collections.Generic.Collection<Cecil.MethodReference> overrides = method.Overrides;

                foreach (Cecil.MethodReference implemented in overrides)
                {
                    AnalysisNet.Types.IMethodReference implementedMethod = ExtractMethod(implemented);
                    AnalysisNet.Types.MethodImplementation methodImplementation = new AnalysisNet.Types.MethodImplementation(implementedMethod, implementingMethod);
                    type.ExplicitOverrides.Add(methodImplementation);
                }
            }
        }

        private void ExtractFields(AnalysisNet.Types.TypeDefinition containingType, IList<AnalysisNet.Types.FieldDefinition> dest, IEnumerable<Cecil.FieldDefinition> source)
        {
            foreach (Cecil.FieldDefinition fielddef in source)
            {
                string name = fielddef.Name;
                AnalysisNet.Types.IType type = ExtractType(fielddef.FieldType);
                AnalysisNet.Types.FieldDefinition field = new AnalysisNet.Types.FieldDefinition(name, type);

                byte[] newArray = new byte[fielddef.InitialValue.Length];
                Array.Copy(fielddef.InitialValue, newArray, newArray.Length);
                field.InitialValue = newArray;

                ExtractCustomAttributes(field.Attributes, fielddef.CustomAttributes);

                field.Visibility = ExtractVisibilityKind(fielddef);
                field.IsStatic = fielddef.IsStatic;
                field.ContainingType = containingType;
                dest.Add(field);
            }
        }
        public AnalysisNet.Types.FieldReference ExtractField(Cecil.FieldReference field, bool isStatic)
        {
            (Cecil.FieldReference, bool) key = ValueTuple.Create(field, isStatic);
            return performanceCache.GetOrCreate(key, (cacheEntry) =>
            {
                AnalysisNet.Types.FieldReference analysisNetField = new AnalysisNet.Types.FieldReference(field.Name, ExtractType(field.FieldType))
                {
                    ContainingType = (AnalysisNet.Types.IBasicType)ExtractType(field.DeclaringType),
                    IsStatic = isStatic
                };
                return analysisNetField;
            });
        }
        private AnalysisNet.Types.VisibilityKind ExtractVisibilityKind(Cecil.FieldReference field)
        {
            return AnalysisNet.Types.VisibilityKind.Public;
        }
        private void ExtractInterfaces(IList<AnalysisNet.Types.IBasicType> dest, IEnumerable<Cecil.InterfaceImplementation> source)
        {
            foreach (Cecil.InterfaceImplementation interfaceref in source)
            {
                AnalysisNet.Types.IBasicType type = ExtractType(interfaceref.InterfaceType) as AnalysisNet.Types.IBasicType;

                dest.Add(type);
            }
        }
        private void ExtractGenericTypeParameters(AnalysisNet.Types.IGenericDefinition definingType, Cecil.TypeDefinition typedef)
        {
            for (int i = 0; i < typedef.GenericParameters.Count; ++i)
            {
                Cecil.GenericParameter parameterdef = typedef.GenericParameters[i];
                ushort index = (ushort)i;
                string name = parameterdef.Name;
                AnalysisNet.Types.TypeKind typeKind = GetGenericParameterTypeKind(parameterdef);
                AnalysisNet.Types.GenericParameter parameter = new AnalysisNet.Types.GenericParameter(AnalysisNet.Types.GenericParameterKind.Type, index, name, typeKind);

                ExtractCustomAttributes(parameter.Attributes, parameterdef.CustomAttributes);

                parameter.GenericContainer = definingType;
                definingType.GenericParameters.Add(parameter);

                parameter.Constraints.AddRange(parameterdef.Constraints.Select(c => ExtractType(c.ConstraintType)));
            }
        }
        private AnalysisNet.Types.TypeKind GetGenericParameterTypeKind(Cecil.GenericParameter parameterdef)
        {
            AnalysisNet.Types.TypeKind result;
            if (parameterdef.IsValueType)
            {
                result = AnalysisNet.Types.TypeKind.ValueType;
            }
            else
            {
                result = AnalysisNet.Types.TypeKind.ReferenceType;
            }

            return result;
        }

        private void ExtractCustomAttributes(ISet<AnalysisNet.Types.CustomAttribute> dest, IEnumerable<Cecil.CustomAttribute> source)
        {
            foreach (Cecil.CustomAttribute attrib in source)
            {
                AnalysisNet.Types.CustomAttribute attribute = new AnalysisNet.Types.CustomAttribute
                {
                    Type = ExtractType(attrib.AttributeType),
                    Constructor = ExtractMethod(attrib.Constructor)
                };

                ExtractArguments(attribute.Arguments, attrib.ConstructorArguments);

                dest.Add(attribute);
            }
        }

        private void ExtractArguments(IList<AnalysisNet.ThreeAddressCode.Values.Constant> dest, IEnumerable<Cecil.CustomAttributeArgument> source)
        {
            foreach (Cecil.CustomAttributeArgument mexpr in source)
            {
                AnalysisNet.ThreeAddressCode.Values.Constant argument = new AnalysisNet.ThreeAddressCode.Values.Constant(mexpr.Value)
                {
                    Type = ExtractType(mexpr.Type)
                };

                dest.Add(argument);
            }
        }
        private AnalysisNet.Types.ArrayType ExtractType(Cecil.ArrayType typeref)
        {
            AnalysisNet.Types.IType elements = ExtractType(typeref.ElementType);
            AnalysisNet.Types.ArrayType type = new AnalysisNet.Types.ArrayType(elements, (uint)typeref.Rank);

            return type;
        }

        private AnalysisNet.Types.PointerType ExtractType(Cecil.ByReferenceType typeref)
        {
            AnalysisNet.Types.IType target = ExtractType(typeref.ElementType);
            AnalysisNet.Types.PointerType type = new AnalysisNet.Types.PointerType(target);

            return type;
        }
        private AnalysisNet.Types.PointerType ExtractType(Cecil.PointerType typeref)
        {
            AnalysisNet.Types.IType target = ExtractType(typeref.ElementType);
            AnalysisNet.Types.PointerType type = new AnalysisNet.Types.PointerType(target);

            return type;
        }

        private AnalysisNet.Types.IGenericParameterReference ExtractType(Cecil.GenericParameter typeref)
        {
            return genericParameterExtractor.GetGenericParameter(typeref);
        }

        private AnalysisNet.Types.IGenericReference ExtractOwner(Cecil.IGenericParameterProvider provider)
        {
            if (provider is Cecil.TypeReference typeRef)
            {
                return ExtractType(typeRef) as AnalysisNet.Types.IBasicType;
            }
            else
            {
                return ExtractMethod(provider as Cecil.MethodReference);
            }
        }

        private AnalysisNet.Types.FunctionPointerType ExtractType(Cecil.FunctionPointerType typeref)
        {
            AnalysisNet.Types.IType returnType = ExtractType(typeref.ElementType);
            AnalysisNet.Types.FunctionPointerType type = new AnalysisNet.Types.FunctionPointerType(returnType);

            //ExtractCustomAttributes(type.Attributes, typeref.Attr);
            ExtractParameters(type.Parameters, typeref.Parameters);

            //type.IsStatic = typeref.IsStatic;
            type.IsStatic = !(typeref.HasThis || typeref.ExplicitThis);
            return type;
        }

        private void ExtractParameters(ICollection<AnalysisNet.Types.IMethodParameterReference> dest, IEnumerable<Cecil.ParameterDefinition> source)
        {
            foreach (Cecil.ParameterDefinition parameterref in source)
            {
                AnalysisNet.Types.IType type = ExtractType(parameterref.ParameterType);
                AnalysisNet.Types.MethodParameterReference parameter = new AnalysisNet.Types.MethodParameterReference((ushort)parameterref.Index, type)
                {
                    Kind = GetMethodParameterKind(parameterref)
                };
                dest.Add(parameter);
            }
        }

        private AnalysisNet.Types.MethodParameterKind GetMethodParameterKind(Cecil.ParameterDefinition parameterref)
        {
            if (parameterref.IsIn)
            {
                return AnalysisNet.Types.MethodParameterKind.In;
            }
            else if (parameterref.IsOut)
            {
                return AnalysisNet.Types.MethodParameterKind.Out;
            }
            else if (parameterref.ParameterType.IsByReference)
            {
                return AnalysisNet.Types.MethodParameterKind.Ref;
            }
            else
            {
                return AnalysisNet.Types.MethodParameterKind.Normal;
            }
        }
        private void ExtractParameters(IList<AnalysisNet.Types.MethodParameter> dest, IEnumerable<Cecil.ParameterDefinition> source)
        {
            foreach (Cecil.ParameterDefinition parameterdef in source)
            {
                string name = parameterdef.Name;
                AnalysisNet.Types.IType type = ExtractType(parameterdef.ParameterType);
                AnalysisNet.Types.MethodParameter parameter = new AnalysisNet.Types.MethodParameter((ushort)parameterdef.Index, name, type);

                ExtractCustomAttributes(parameter.Attributes, parameterdef.CustomAttributes);

                if (parameterdef.HasConstant)
                {
                    parameter.DefaultValue = new AnalysisNet.ThreeAddressCode.Values.Constant(parameterdef.Constant)
                    {
                        Type = parameter.Type
                    };
                }

                parameter.Kind = GetMethodParameterKind(parameterdef);
                dest.Add(parameter);
            }
        }

        private AnalysisNet.Types.TypeKind GetTypeKind(Cecil.TypeReference typeref)
        {
            AnalysisNet.Types.TypeKind result = AnalysisNet.Types.TypeKind.Unknown;

            if (typeref.IsValueType)
            {
                result = AnalysisNet.Types.TypeKind.ValueType;
            }
            else
            {
                result = AnalysisNet.Types.TypeKind.ReferenceType;
            }

            return result;
        }
        private string UnmangleName(Cecil.MemberReference member)
        {
            int lastIdx = member.Name.IndexOf('`');
            if (lastIdx == -1)
            {
                return member.Name;
            }

            string substring = member.Name.Substring(0, lastIdx);
            return substring;
        }
        private AnalysisNet.Types.IType ExtractNonGenericInstanceType(Cecil.TypeReference typeref)
        {
            if (typeref.IsGenericInstance)
            {
                throw new Exception("precondition violation");
            }


            // we don't want the file extension
            string containingAssembly;
            if (typeref.Scope is Cecil.AssemblyNameReference assemblyRef)
            {
                containingAssembly = assemblyRef.Name;
            }
            else if (typeref.Scope is Cecil.ModuleDefinition moduleDef)
            {
                containingAssembly = moduleDef.Assembly.Name.Name;
            }
            else
            {
                throw new NotImplementedException();
            }

            string containingNamespace = typeref.Namespace;
            string name = UnmangleName(typeref);
            AnalysisNet.Types.TypeKind kind = GetTypeKind(typeref);
            AnalysisNet.Types.BasicType newType = new AnalysisNet.Types.BasicType(name, kind)
            {

                //ExtractAttributes(newType.Attributes, typeref.Attributes);

                ContainingAssembly = new AnalysisNet.AssemblyReference(containingAssembly),
                ContainingNamespace = containingNamespace,

                GenericParameterCount = typeref.GenericParameters.Count
            };

            genericParameterExtractor.MapGenericParameters(typeref, newType);

            if (typeref.IsNested)
            {
                newType.ContainingType = (AnalysisNet.Types.IBasicType)ExtractType(typeref.DeclaringType);
                // analysis-net does not follow ecma
                // it expects nested types and their enclosing type share the same namespace
                newType.ContainingNamespace = newType.ContainingType.ContainingNamespace;
            }

            return newType;
        }

        private AnalysisNet.Types.IType ExtractType(Cecil.GenericInstanceType typeref)
        {
            AnalysisNet.Types.BasicType genericTyperef = (AnalysisNet.Types.BasicType)ExtractType(typeref.ElementType);
            AnalysisNet.Types.IType[] arguments = typeref.GenericArguments.Select(argumentref => ExtractType(argumentref)).ToArray();
            AnalysisNet.Types.BasicType instancedType = AnalysisNet.Extensions.Instantiate(genericTyperef, arguments);
            instancedType.Resolve(host);

            return instancedType;
        }

        public AnalysisNet.Types.IType ExtractType(Cecil.TypeReference typeReference)
        {
            return performanceCache.GetOrCreate(typeReference, (cacheEntry) =>
            {
                AnalysisNet.Types.IType result = null;

                if (typeReference is Cecil.ArrayType arrayType)
                {
                    result = ExtractType(arrayType);
                }
                else if (typeReference is Cecil.ByReferenceType byReferenceType)
                {
                    result = ExtractType(byReferenceType);
                }
                else if (typeReference is Cecil.PointerType pointerType)
                {
                    result = ExtractType(pointerType);
                }
                else if (typeReference is Cecil.GenericParameter genericParameterType)
                {
                    result = ExtractType(genericParameterType);
                }
                else if (typeReference is Cecil.FunctionPointerType functionPointerType)
                {
                    result = ExtractType(functionPointerType);
                }
                else if (typeReference is Cecil.GenericInstanceType genericInstanceType)
                {
                    result = ExtractType(genericInstanceType);
                }
                else
                {
                    // named type reference
                    result = ExtractNonGenericInstanceType(typeReference);
                }

                if (result is AnalysisNet.Types.BasicType)
                {
                    AnalysisNet.Types.BasicType basicType = result as AnalysisNet.Types.BasicType;
                    basicType.Resolve(host);

                    if (basicType.GenericType is AnalysisNet.Types.BasicType)
                    {
                        basicType = basicType.GenericType as AnalysisNet.Types.BasicType;
                        basicType.Resolve(host);
                    }
                }

                return result;
            });
        }

        public AnalysisNet.Types.IMetadataReference ExtractToken(Cecil.MemberReference token)
        {
            AnalysisNet.Types.IMetadataReference result = AnalysisNet.Types.PlatformTypes.Unknown;

            if (token is Cecil.MethodReference methodRef)
            {
                result = ExtractMethod(methodRef);
            }
            else if (token is Cecil.TypeReference typeRef)
            {
                result = ExtractType(typeRef);
            }
            else if (token is Cecil.FieldReference fieldRef)
            {
                // not sure about it
                bool isStatic = fieldRef.IsDefinition ? fieldRef.Resolve().IsStatic : true;
                result = ExtractField(fieldRef, isStatic);
            }
            else
            {
                throw new NotImplementedException();
            }

            return result;
        }
        public AnalysisNet.Types.IMethodReference ExtractMethod(Cecil.MethodReference methodReference)
        {
            return performanceCache.GetOrCreate(methodReference, (cacheEntry) =>
            {
                if (methodReference is Cecil.GenericInstanceMethod instanceMethod)
                {
                    List<AnalysisNet.Types.IType> genericArguments = new List<AnalysisNet.Types.IType>();

                    foreach (Cecil.TypeReference typeParameterref in instanceMethod.GenericArguments)
                    {
                        AnalysisNet.Types.IType typeArgumentref = ExtractType(typeParameterref);
                        genericArguments.Add(typeArgumentref);
                    }

                    AnalysisNet.Types.IMethodReference method = ExtractMethod(instanceMethod.GetElementMethod());
                    AnalysisNet.Types.MethodReference instantiatedMethod = AnalysisNet.Extensions.Instantiate(method, genericArguments);
                    instantiatedMethod.Resolve(host);

                    return instantiatedMethod;
                }
                else
                {
                    return ExtractNonGenericInstanceMethod(methodReference);
                }
            });
        }

        private AnalysisNet.Types.IMethodReference ExtractNonGenericInstanceMethod(Cecil.MethodReference methodref)
        {
            AnalysisNet.Types.IType extractedType = ExtractType(methodref.DeclaringType);
            AnalysisNet.Types.IBasicType containingType;
            if (extractedType is AnalysisNet.Types.ArrayType arrayType)
            {
                containingType = new FakeArrayType(arrayType);
            }
            else
            {
                containingType = (AnalysisNet.Types.IBasicType)extractedType;
            }

            AnalysisNet.Types.MethodReference method = new AnalysisNet.Types.MethodReference(methodref.Name, AnalysisNet.Types.PlatformTypes.Void);
            genericParameterExtractor.MapGenericParameters(methodref, method);
            method.ReturnType = ExtractType(methodref.ReturnType);

            ExtractParameters(method.Parameters, methodref.Parameters);

            method.GenericParameterCount = methodref.GenericParameters.Count();
            method.ContainingType = containingType;
            method.IsStatic = !(methodref.HasThis || methodref.ExplicitThis);

            method.Resolve(host);
            return method;
        }
        private AnalysisNet.Types.VisibilityKind ExtractVisibilityKind(Cecil.MethodReference method)
        {
            return AnalysisNet.Types.VisibilityKind.Public;
        }
        private void ExtractMethods(AnalysisNet.Types.TypeDefinition containingType, IList<AnalysisNet.Types.MethodDefinition> dest, IEnumerable<Cecil.MethodDefinition> source)
        {
            foreach (Cecil.MethodDefinition methoddef in source)
            {
                string name = methoddef.Name;
                AnalysisNet.Types.MethodDefinition method = new AnalysisNet.Types.MethodDefinition(name, null);

                ExtractCustomAttributes(method.Attributes, methoddef.CustomAttributes);
                ExtractGenericMethodParameters(method, methoddef);
                ExtractParameters(method.Parameters, methoddef.Parameters);

                method.ReturnType = ExtractType(methoddef.ReturnType);

                if (methoddef.HasBody)
                {
                    CodeProvider codeProvider = new CodeProvider(this);
                    method.Body = codeProvider.ExtractBody(methoddef.Body);
                }

                method.Visibility = ExtractVisibilityKind(methoddef);
                method.IsStatic = methoddef.IsStatic;
                method.IsAbstract = methoddef.IsAbstract;
                method.IsVirtual = methoddef.IsVirtual;
                method.IsOverrider = (methoddef.IsAbstract || methoddef.IsVirtual) && !methoddef.IsNewSlot;
                method.IsFinal = methoddef.IsFinal;
                method.IsConstructor = methoddef.IsConstructor;
                method.IsExternal = methoddef.IsPInvokeImpl;
                method.ContainingType = containingType;
                dest.Add(method);
            }
        }

        private void ExtractGenericMethodParameters(AnalysisNet.Types.MethodDefinition method, Cecil.MethodDefinition methoddef)
        {
            foreach (Cecil.GenericParameter cecilParam in methoddef.GenericParameters)
            {
                AnalysisNet.Types.GenericParameter analysisNetParam = new AnalysisNet.Types.GenericParameter(AnalysisNet.Types.GenericParameterKind.Method, (ushort)cecilParam.Position, cecilParam.Name, GetGenericParameterTypeKind(cecilParam))
                {
                    GenericContainer = method
                };
                method.GenericParameters.Add(analysisNetParam);
                ExtractCustomAttributes(analysisNetParam.Attributes, cecilParam.CustomAttributes);

                IEnumerable<AnalysisNet.Types.IType> constraints = cecilParam.Constraints.Select(cecilConst => ExtractType(cecilConst.ConstraintType));
                analysisNetParam.Constraints.AddRange(constraints);
            }
        }
    }
}
