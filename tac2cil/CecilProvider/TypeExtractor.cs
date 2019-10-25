using System;
using System.Collections.Generic;
using System.Text;

using Cecil = Mono.Cecil;
using AnalysisNet = Model;
using System.Linq;

namespace CecilProvider
{

    class TypeExtractor
    {
        class GenericParameterExtractor
        {
            public GenericParameterExtractor(TypeExtractor typeExtractor)
            {
                this.typeExtractor = typeExtractor;
                this.genericParamsMap = new Dictionary<Cecil.IGenericParameterProvider, GenericParameterMap>();
            }

            private class GenericParameterMap : Dictionary<int, AnalysisNet.Types.IGenericParameterReference> { }
            private readonly IDictionary<Cecil.IGenericParameterProvider, GenericParameterMap> genericParamsMap;
            TypeExtractor typeExtractor;

            private AnalysisNet.Types.GenericParameterKind GetKind(Cecil.GenericParameter cecilContainer)
            {
                if (cecilContainer.Type == Cecil.GenericParameterType.Type)
                    return AnalysisNet.Types.GenericParameterKind.Type;
                else if (cecilContainer.Type == Cecil.GenericParameterType.Method)
                    return AnalysisNet.Types.GenericParameterKind.Method;

                throw new NotImplementedException();
            }
            public void MapGenericParameters(Cecil.IGenericParameterProvider cecilContainer, AnalysisNet.Types.IGenericReference analysisNetContainer)
            {
                var map = new GenericParameterMap();

                for (int i = 0; i < cecilContainer.GenericParameters.Count; i++)
                {
                    var cecilParam = cecilContainer.GenericParameters.ElementAt(i);
                    var analysisNetParam = new AnalysisNet.Types.GenericParameterReference(GetKind(cecilParam), (ushort)cecilParam.Position)
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
                var owner = cecilParameter.Owner;
                if (genericParamsMap.TryGetValue(owner, out GenericParameterMap map))
                    return map[cecilParameter.Position];

                // populates map
                if (cecilParameter.Type == Cecil.GenericParameterType.Method)
                    typeExtractor.ExtractMethod(owner as Cecil.MethodReference);
                else
                {
                    // check my assumption
                    if ((owner is Cecil.GenericInstanceType))
                        throw new Exception();

                    typeExtractor.ExtractType(owner as Cecil.TypeReference);
                }

                map = genericParamsMap[owner];
                return map[cecilParameter.Position];
            }
        }
        private AnalysisNet.Host host;
        private GenericParameterExtractor genericParameterExtractor;
        // usted to prevent infinite recursion while creating generic parameter references

        public TypeExtractor(AnalysisNet.Host host)
        {
            this.host = host;
            this.genericParameterExtractor = new GenericParameterExtractor(this);
        }

        private bool IsDelegate(Cecil.TypeDefinition cecilType)
        {
            var baseType = cecilType.BaseType;

            if (baseType == null)
                return false;
            
            var coreLibrary = cecilType.Module.TypeSystem.CoreLibrary;

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
            } else if (cecilType.IsValueType)
            {
                result = ExtractClass(cecilType, AnalysisNet.Types.TypeKind.ValueType, AnalysisNet.Types.TypeDefinitionKind.Struct);
            }
            else if (cecilType.IsClass)
            {
                // includes delegates!
                var kind = IsDelegate(cecilType) ? AnalysisNet.Types.TypeDefinitionKind.Delegate : AnalysisNet.Types.TypeDefinitionKind.Class;
                result = ExtractClass(cecilType, AnalysisNet.Types.TypeKind.ReferenceType, kind);
            }
            else if (cecilType.IsInterface)
            {
                result = ExtractInterface(cecilType);
            }
            else
                throw new NotImplementedException();

            return result;
        }
        private AnalysisNet.Types.TypeDefinition ExtractEnum(Cecil.TypeDefinition typedef)
        {
            var name = typedef.Name;
            var type = new AnalysisNet.Types.TypeDefinition(name, AnalysisNet.Types.TypeKind.ValueType, AnalysisNet.Types.TypeDefinitionKind.Enum);
            type.Base = ExtractType(typedef.BaseType) as AnalysisNet.Types.IBasicType;
            type.IsAbstract = typedef.IsAbstract;
            type.IsSealed = typedef.IsSealed;
            var valueField = typedef.Fields.Single(f => f.Name == "value__");
            type.UnderlayingType = ExtractType(valueField.FieldType) as AnalysisNet.Types.IBasicType;

            ExtractCustomAttributes(type.Attributes, typedef.CustomAttributes);
            ExtractConstants(type, type.Fields, typedef.Fields);

            return type;
        }

        private void ExtractConstants(AnalysisNet.Types.TypeDefinition containingType, IList<AnalysisNet.Types.FieldDefinition> dest, IEnumerable<Cecil.FieldDefinition> source)
        {
            source = source.Skip(1);

            foreach (var constdef in source)
            {
                if (!constdef.HasConstant)
                    continue;

                var name = constdef.Name;
                var constant = new AnalysisNet.Types.FieldDefinition(name, containingType)
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
            foreach (var cecilProperty in cecilType.Properties)
            {
                var ourProp = new AnalysisNet.Types.PropertyDefinition(cecilProperty.Name, ExtractType(cecilProperty.PropertyType))
                {
                    ContainingType = analysisNetType
                };

                if (cecilProperty.GetMethod != null)
                {
                    // It is a reference but we need the definition.
                    // It is not safe to call ResolvedMethod at this point, the model is incomplete.
                    var getterRef = ExtractMethod(cecilProperty.GetMethod);
                    ourProp.Getter = analysisNetType.Methods.Where(methodDef => methodDef.MatchSignature(getterRef)).First();
                }
                if (cecilProperty.SetMethod != null)
                {
                    // It is a reference but we need the definition.
                    // It is not safe to call ResolvedMethod at this point, the model is incomplete.
                    var setterRef = ExtractMethod(cecilProperty.SetMethod);
                    ourProp.Setter = analysisNetType.Methods.Where(methodDef => methodDef.MatchSignature(setterRef)).First();
                }

                ExtractCustomAttributes(ourProp.Attributes, cecilProperty.CustomAttributes);
                analysisNetType.PropertyDefinitions.Add(ourProp);
            }
        }
        private AnalysisNet.Types.TypeDefinition ExtractInterface(Cecil.TypeDefinition cecilType)
        {
            var name = UnmangleName(cecilType);
            var type = new AnalysisNet.Types.TypeDefinition(name, AnalysisNet.Types.TypeKind.ReferenceType, AnalysisNet.Types.TypeDefinitionKind.Interface);

            type.IsAbstract = cecilType.IsAbstract;
            type.IsSealed = cecilType.IsSealed;

            ExtractCustomAttributes(type.Attributes, cecilType.CustomAttributes);
            ExtractGenericTypeParameters(type, cecilType);
            ExtractInterfaces(type.Interfaces, cecilType.Interfaces);
            ExtractMethods(type, type.Methods, cecilType.Methods);
            ExtractPropertyDefinitions(type, cecilType);
            return type;
        }

        private AnalysisNet.Types.TypeDefinition ExtractClass(Cecil.TypeDefinition cecilType, AnalysisNet.Types.TypeKind typeKind, AnalysisNet.Types.TypeDefinitionKind typeDefinitionKind)
        {
            var name = UnmangleName(cecilType);
            var type = new AnalysisNet.Types.TypeDefinition(name, typeKind, typeDefinitionKind);
            var basedef = cecilType.BaseType;

            type.IsAbstract = cecilType.IsAbstract;
            type.IsSealed = cecilType.IsSealed;

            if (basedef != null)
                type.Base = ExtractType(basedef) as AnalysisNet.Types.IBasicType;

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
                kind = AnalysisNet.Types.LayoutKind.AutoLayout;
            else if (typeDefinition.IsExplicitLayout)
                kind = AnalysisNet.Types.LayoutKind.ExplicitLayout;
            else if (typeDefinition.IsSequentialLayout)
                kind = AnalysisNet.Types.LayoutKind.SequentialLayout;
            else
                throw new NotImplementedException();

            Model.Types.LayoutInformation layoutInformation = new AnalysisNet.Types.LayoutInformation(kind)
            {
                ClassSize = typeDefinition.ClassSize,
                PackingSize = typeDefinition.PackingSize
            };

            type.LayoutInformation = layoutInformation;
        }

        private void ExtractExplicitMethodOverrides(AnalysisNet.Types.TypeDefinition type, Cecil.TypeDefinition typeDefinition)
        {
            foreach (var method in typeDefinition.Methods)
            {
                var implementingMethod = ExtractMethod(method);
                var overrides = method.Overrides;

                foreach (var implemented in overrides)
                {
                    var implementedMethod = ExtractMethod(implemented);
                    var methodImplementation = new AnalysisNet.Types.MethodImplementation(implementedMethod, implementingMethod);
                    type.ExplicitOverrides.Add(methodImplementation);
                }
            }
        }

        private void ExtractFields(AnalysisNet.Types.TypeDefinition containingType, IList<AnalysisNet.Types.FieldDefinition> dest, IEnumerable<Cecil.FieldDefinition> source)
        {
            foreach (var fielddef in source)
            {
                var name = fielddef.Name;
                var type = ExtractType(fielddef.FieldType);
                var field = new AnalysisNet.Types.FieldDefinition(name, type);

                var newArray = new byte[fielddef.InitialValue.Length];
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
            var analysisNetField = new AnalysisNet.Types.FieldReference(field.Name, ExtractType(field.FieldType));
            analysisNetField.ContainingType = (AnalysisNet.Types.IBasicType)ExtractType(field.DeclaringType);
            analysisNetField.IsStatic = isStatic;
            return analysisNetField;
        }
        private AnalysisNet.Types.VisibilityKind ExtractVisibilityKind(Cecil.FieldReference field)
        {
            return AnalysisNet.Types.VisibilityKind.Public;
        }
        private void ExtractInterfaces(IList<AnalysisNet.Types.IBasicType> dest, IEnumerable<Cecil.InterfaceImplementation> source)
        {
            foreach (var interfaceref in source)
            {
                var type = ExtractType(interfaceref.InterfaceType) as AnalysisNet.Types.IBasicType;

                dest.Add(type);
            }
        }
        private void ExtractGenericTypeParameters(AnalysisNet.Types.IGenericDefinition definingType, Cecil.TypeDefinition typedef)
        {
            for (var i = 0; i < typedef.GenericParameters.Count; ++i)
            {
                var parameterdef = typedef.GenericParameters[i];
                var index = (ushort)i;
                var name = parameterdef.Name;
                var typeKind = GetGenericParameterTypeKind(parameterdef); 
                var parameter = new AnalysisNet.Types.GenericParameter(AnalysisNet.Types.GenericParameterKind.Type, index, name, typeKind);

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
                result = AnalysisNet.Types.TypeKind.ValueType;
            else
                result = AnalysisNet.Types.TypeKind.ReferenceType;

            return result;
        }

        private void ExtractCustomAttributes(ISet<AnalysisNet.Types.CustomAttribute> dest, IEnumerable<Cecil.CustomAttribute> source)
        {
            foreach (var attrib in source)
            {
                var attribute = new AnalysisNet.Types.CustomAttribute();

                attribute.Type = ExtractType(attrib.AttributeType);
                attribute.Constructor = ExtractMethod(attrib.Constructor);

                ExtractArguments(attribute.Arguments, attrib.ConstructorArguments);

                dest.Add(attribute);
            }
        }

        private void ExtractArguments(IList<AnalysisNet.ThreeAddressCode.Values.Constant> dest, IEnumerable<Cecil.CustomAttributeArgument> source)
        {
            foreach (var mexpr in source)
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
            var elements = ExtractType(typeref.ElementType);
            var type = new AnalysisNet.Types.ArrayType(elements, (uint)typeref.Rank);

            //ExtractAttributes(type.Attributes, typeref.);

            return type;
        }

        private AnalysisNet.Types.PointerType ExtractType(Cecil.ByReferenceType typeref)
        {
            var target = ExtractType(typeref.ElementType);
            var type = new AnalysisNet.Types.PointerType(target);

            //ExtractAttributes(type.Attributes, typeref.Attributes);

            return type;
        }
        private AnalysisNet.Types.PointerType ExtractType(Cecil.PointerType typeref)
        {
            var target = ExtractType(typeref.ElementType);
            var type = new AnalysisNet.Types.PointerType(target);

            //ExtractAttributes(type.Attributes, typeref.Attributes);

            return type;
        }

        private AnalysisNet.Types.IGenericParameterReference ExtractType(Cecil.GenericParameter typeref)
        {
            //var containingType = GetContainingType(typeref.DefiningType);
            //var startIndex = TotalGenericParameterCount(containingType);
            //var index = startIndex + typeref.Index;
            //return genericContext.TypeParameters[index];

            /*AnalysisNet.Types.GenericParameterKind kind = typeref.Type == Cecil.GenericParameterType.Type ? 
                AnalysisNet.Types.GenericParameterKind.Type : AnalysisNet.Types.GenericParameterKind.Method;

            AnalysisNet.Types.GenericParameterReference genericParameterReference = new AnalysisNet.Types.GenericParameterReference(kind, (ushort)typeref.Position);
            genericParameterReference.GenericContainer = ExtractOwner(typeref.Owner);

            ExtractCustomAttributes(genericParameterReference.Attributes, typeref.CustomAttributes);*/

            return genericParameterExtractor.GetGenericParameter(typeref);
        }

        private AnalysisNet.Types.IGenericReference ExtractOwner(Cecil.IGenericParameterProvider provider)
        {
            if (provider is Cecil.TypeReference typeRef)
                return ExtractType(typeRef) as AnalysisNet.Types.IBasicType;
            else
                return ExtractMethod(provider as Cecil.MethodReference);
        }

        private AnalysisNet.Types.FunctionPointerType ExtractType(Cecil.FunctionPointerType typeref)
        {
            var returnType = ExtractType(typeref.ElementType);
            var type = new AnalysisNet.Types.FunctionPointerType(returnType);

            //ExtractCustomAttributes(type.Attributes, typeref.Attr);
            ExtractParameters(type.Parameters, typeref.Parameters);

            //type.IsStatic = typeref.IsStatic;
            type.IsStatic = !(typeref.HasThis || typeref.ExplicitThis);
            return type;
        }

        private void ExtractParameters(ICollection<AnalysisNet.Types.IMethodParameterReference> dest, IEnumerable<Cecil.ParameterDefinition> source)
        {
            foreach (var parameterref in source)
            {
                var type = ExtractType(parameterref.ParameterType);
                var parameter = new AnalysisNet.Types.MethodParameterReference((ushort)parameterref.Index, type);

                parameter.Kind = GetMethodParameterKind(parameterref);
                dest.Add(parameter);
            }
        }

        private AnalysisNet.Types.MethodParameterKind GetMethodParameterKind(Cecil.ParameterDefinition parameterref)
        {
            if (parameterref.IsIn)
                return AnalysisNet.Types.MethodParameterKind.In;
            else if (parameterref.IsOut)
                return AnalysisNet.Types.MethodParameterKind.Out;
            else if (parameterref.ParameterType.IsByReference)
                return AnalysisNet.Types.MethodParameterKind.Ref;
            else
                return AnalysisNet.Types.MethodParameterKind.Normal;
        }
        private void ExtractParameters(IList<AnalysisNet.Types.MethodParameter> dest, IEnumerable<Cecil.ParameterDefinition> source)
        {
            foreach (var parameterdef in source)
            {
                var name = parameterdef.Name;
                var type = ExtractType(parameterdef.ParameterType);
                var parameter = new AnalysisNet.Types.MethodParameter((ushort)parameterdef.Index, name, type);

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
            var result = AnalysisNet.Types.TypeKind.Unknown;

            if (typeref.IsValueType) result = AnalysisNet.Types.TypeKind.ValueType;
            else result = AnalysisNet.Types.TypeKind.ReferenceType;

            return result;
        }
        private string UnmangleName(Cecil.MemberReference member)
        {
            var lastIdx = member.Name.IndexOf('`');
            if (lastIdx == -1)
                return member.Name;
            var substring = member.Name.Substring(0, lastIdx);
            return substring;
        }
        private AnalysisNet.Types.IType ExtractNonGenericInstanceType(Cecil.TypeReference typeref)
        {
            if (typeref.IsGenericInstance)
                throw new Exception("precondition violation");


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
                throw new NotImplementedException();

            string containingNamespace = typeref.Namespace;
            string name = UnmangleName(typeref);
            var kind = GetTypeKind(typeref);
            var newType = new AnalysisNet.Types.BasicType(name, kind);

            //ExtractAttributes(newType.Attributes, typeref.Attributes);

            newType.ContainingAssembly = new AnalysisNet.AssemblyReference(containingAssembly);
            newType.ContainingNamespace = containingNamespace;

            newType.GenericParameterCount = typeref.GenericParameters.Count;

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
            var genericTyperef = typeref.ElementType;

            // cuidado si cacheamos porque aca estariamos modificando algo del cache
            var instancedType = (AnalysisNet.Types.BasicType)ExtractType(genericTyperef);
            var genericType = (AnalysisNet.Types.IBasicType)ExtractType(genericTyperef);
            instancedType.GenericType = genericType;

            foreach (var argumentref in typeref.GenericArguments)
            {
                var typearg = ExtractType(argumentref);
                instancedType.GenericArguments.Add(typearg);
            }

            return instancedType;
        }

        public AnalysisNet.Types.IType ExtractType(Cecil.TypeReference typeReference)
        {
            AnalysisNet.Types.IType result = null;

            if (typeReference is Cecil.ArrayType arrayType)
            {
                result = ExtractType(arrayType);
            } else if (typeReference is Cecil.ByReferenceType byReferenceType)
            {
                result = ExtractType(byReferenceType);
            } else if (typeReference is Cecil.PointerType pointerType)
            {
                result = ExtractType(pointerType);
            } else if (typeReference is Cecil.GenericParameter genericParameterType)
            {
                result = ExtractType(genericParameterType);
            } else if (typeReference is Cecil.FunctionPointerType functionPointerType)
            {
                result = ExtractType(functionPointerType);
            } else if (typeReference is Cecil.GenericInstanceType genericInstanceType)
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
                var basicType = result as AnalysisNet.Types.BasicType;
                basicType.Resolve(host);

                if (basicType.GenericType is AnalysisNet.Types.BasicType)
                {
                    basicType = basicType.GenericType as AnalysisNet.Types.BasicType;
                    basicType.Resolve(host);
                }
            }

            return result;
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
                var isStatic = fieldRef.IsDefinition ? fieldRef.Resolve().IsStatic : true;
                result = ExtractField(fieldRef, isStatic);
            }
            else
                throw new NotImplementedException();

            return result;
        }
        public AnalysisNet.Types.IMethodReference ExtractMethod(Cecil.MethodReference methodReference)
        {
            if (methodReference is Cecil.GenericInstanceMethod instanceMethod)
            {
                var genericArguments = new List<AnalysisNet.Types.IType>();

                foreach (var typeParameterref in instanceMethod.GenericArguments)
                {
                    var typeArgumentref = ExtractType(typeParameterref);
                    genericArguments.Add(typeArgumentref);
                }

                //CreateGenericParameterReferences(GenericParameterKind.Method, genericArguments.Count);

                var method = ExtractMethod(instanceMethod.GetElementMethod());
                var instantiatedMethod = AnalysisNet.Extensions.Instantiate(method, genericArguments);
                instantiatedMethod.Resolve(host);

                //BindGenericParameterReferences(GenericParameterKind.Method, instantiatedMethod);
                return instantiatedMethod;
            }
            else
                return ExtractNonGenericInstanceMethod(methodReference);
        }

        private AnalysisNet.Types.IMethodReference ExtractNonGenericInstanceMethod(Cecil.MethodReference methodref)
        {
            var extractedType = ExtractType(methodref.DeclaringType);
            AnalysisNet.Types.IBasicType containingType;
            if (extractedType is AnalysisNet.Types.ArrayType arrayType)
                containingType = new FakeArrayType(arrayType);
            else
                containingType = (AnalysisNet.Types.IBasicType)extractedType;

            var method = new AnalysisNet.Types.MethodReference(methodref.Name, AnalysisNet.Types.PlatformTypes.Void);
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
            foreach (var methoddef in source)
            {
                var name = methoddef.Name;
                var method = new AnalysisNet.Types.MethodDefinition(name, null);

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
            foreach (var cecilParam in methoddef.GenericParameters)
            {
                var analysisNetParam = new AnalysisNet.Types.GenericParameter(AnalysisNet.Types.GenericParameterKind.Method, (ushort)cecilParam.Position, cecilParam.Name, GetGenericParameterTypeKind(cecilParam));
                analysisNetParam.GenericContainer = method;
                method.GenericParameters.Add(analysisNetParam);
                ExtractCustomAttributes(analysisNetParam.Attributes, cecilParam.CustomAttributes);

                var constraints = cecilParam.Constraints.Select(cecilConst => ExtractType(cecilConst.ConstraintType));
                analysisNetParam.Constraints.AddRange(constraints);
            }
        }
    }
}
