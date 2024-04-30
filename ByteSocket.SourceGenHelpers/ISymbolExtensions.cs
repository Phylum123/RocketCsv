using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Immutable;

namespace ByteSocket.SourceGenHelpers
{
    public class CreateObjectOptions
    {
        public CreateObjectOptions(IAssemblySymbol assemblyToCreateIn, IMethodSymbol constructor, IEnumerable<string> constructorArgValues = null, string newObjVarName = "newObject", bool allowIllegalObjCreation = false)
        {
            AssemblyToCreateIn = assemblyToCreateIn ?? throw new ArgumentNullException(nameof(assemblyToCreateIn));
            Constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));

            if (constructor.Parameters.Where(x => !x.HasExplicitDefaultValue).Count() > (constructorArgValues?.Count() ?? 0))
            {
                throw new ArgumentNullException(nameof(constructorArgValues), $"Not enough {nameof(constructorArgValues)} for the selected constructor!");
            }

            if (constructor.Parameters.Count() < (constructorArgValues?.Count() ?? 0))
            {
                throw new ArgumentNullException(nameof(constructorArgValues), $"Too many {nameof(constructorArgValues)} for the selected constructor!");
            }

            ConstructorArgValues = constructorArgValues;
            NewObjVarName = newObjVarName;
            AllowIllegalObjCreation = allowIllegalObjCreation;
        }

        public IAssemblySymbol AssemblyToCreateIn { get; }
        public IMethodSymbol Constructor { get; }
        public IEnumerable<string> ConstructorArgValues { get; }

        private List<(IPropertySymbol PropertySymbol, string Value, bool AllowIllegalSet)> _propertyValues = new List<(IPropertySymbol PropertySymbol, string Value, bool AllowIllegalSet)>();
        public IReadOnlyList<(IPropertySymbol PropertySymbol, string Value, bool AllowIllegalSet)> PropertyValues => _propertyValues;


        public string NewObjVarName { get; set; }
        public bool AllowIllegalObjCreation { get; set; }

        public void AddPropertyValue(IPropertySymbol property, string value, bool allowIllegalSet = false)
        {
            if (property.SetMethod == null)
            {
                throw new ArgumentException($"Property '{property.Name}' does not have a setter.", nameof(property));
            }

            _propertyValues.Add((property, value, allowIllegalSet));
        }
    }


    public static class ISymbolExtensions
    {
        /// <summary>
        /// Selects the best constructor for a class or struct, based on the constructor that has the most parameters that match properties of the class or struct (ignoring case, but matching type)
        /// </summary>
        /// <param name="classOrStruct">The INamedTypeSymbol of the class or struct to get the best constructor for</param>
        /// <param name="paramTypeComparer">Used to determine if the parameter type comparison should include type nullability</param>
        /// <param name="allowedConstructorAccessabilities">Use only constructors with the given accessabilities. If null this, it will default to 'public' only.</param>
        /// <param name="allowedPropertyAccessabilities">Use only properties with the given accessabilities. If null this, it will default to 'public' only.</param>
        /// <param name="propsToIgnore">Any properties you don't want considered to help choose the best constructor</param>
        /// <returns></returns>
        public static (IMethodSymbol BestConstructor, IEnumerable<IPropertySymbol> MatchingProperties) GetBestConstructor(this INamedTypeSymbol classOrStruct, SymbolEqualityComparer paramTypeComparer, IEnumerable<Accessibility> allowedConstructorAccessabilities = null, IEnumerable<Accessibility> allowedPropertyAccessabilities = null,
            IEnumerable<string> propsToIgnore = null)
        {
            allowedConstructorAccessabilities = allowedConstructorAccessabilities ?? new List<Accessibility> { Accessibility.Public };
            allowedPropertyAccessabilities = allowedPropertyAccessabilities ?? new List<Accessibility> { Accessibility.Public };

            var constructors = new Dictionary<IMethodSymbol, List<IPropertySymbol>>(SymbolEqualityComparer.Default);

            foreach (var ctor in classOrStruct.Constructors.Where(x => !x.IsStatic && allowedConstructorAccessabilities.Contains(x.DeclaredAccessibility)))
            {

                if (ctor.Parameters.Count() == 0)
                {
                    constructors.Add(ctor, new List<IPropertySymbol>());
                }
                else
                {
                    var matchingProps = new List<IPropertySymbol>();

                    foreach (var param in ctor.Parameters)
                    {
                        var props = classOrStruct.GetMembers().OfType<IPropertySymbol>().Where(x => allowedPropertyAccessabilities.Contains(x.DeclaredAccessibility) &&
                                                                                                    (propsToIgnore == null ? true : !propsToIgnore.Contains(x.Name)));

                        var matchingProp = props.FirstOrDefault(x => param.Name.Equals(x.Name, StringComparison.OrdinalIgnoreCase) && param.Type.Equals(x.Type, paramTypeComparer));

                        if (matchingProp != null)
                        {
                            matchingProps.Add(matchingProp);
                        }
                        else
                        {
                            //Can't have a single parameter that isn't tied to a property - so this constructor is invalid.
                            matchingProps.Clear();
                            break;
                        }
                    }

                    if (matchingProps.Count > 0)
                    {
                        constructors.Add(ctor, matchingProps);
                    }
                }

            }

            var bestCtor = constructors.OrderByDescending(x => x.Value.Count).FirstOrDefault();

            return constructors.Count > 0 ? (bestCtor.Key, bestCtor.Value) : default;
        }



        public static string CreateObject(this INamedTypeSymbol classOrStruct, CreateObjectOptions createObjectOptions, out string staticDelegateMethods, out string staticDelegateVars)
        {
            createObjectOptions = createObjectOptions ?? throw new ArgumentNullException(nameof(createObjectOptions));

            if (!classOrStruct.Constructors.Contains(createObjectOptions.Constructor))
            {
                throw new ArgumentException("Constructor is not a constructor of the given class or struct.", nameof(classOrStruct));
            }

            var canAccessObj = createObjectOptions.AssemblyToCreateIn.CanAccess(classOrStruct) &&
                               createObjectOptions.AssemblyToCreateIn.CanAccess(createObjectOptions.Constructor);

            if (!canAccessObj && createObjectOptions.Constructor.Parameters.Length > 16 && createObjectOptions.AllowIllegalObjCreation)
            {
                throw new ArgumentException($"Constructor can have a maximum of 16 parameters, when using the '{nameof(createObjectOptions.AllowIllegalObjCreation)}' option and illegal object creation is required. This limitation is due to the number of parameters the 'Func' class supports.", nameof(classOrStruct));
            }

            StringBuilder sbDelegateCreateMethods = new StringBuilder();
            StringBuilder sbDelegateVars = new StringBuilder();
            StringBuilder sbObjectCreation = new StringBuilder();

            if (!canAccessObj)
            {
                if (createObjectOptions.AllowIllegalObjCreation)
                {
                    sbDelegateCreateMethods.AppendLine(GetDelegateCreateMethod(createObjectOptions.Constructor.Parameters));

                    if (createObjectOptions.Constructor.Parameters.Length == 0)
                        sbDelegateVars.AppendLine($"private static Func<{classOrStruct.Name}> _{classOrStruct.Name}_CreateDelegate = GetCreateDelegate<{classOrStruct.Name}>();");
                    else
                    {
                        var paramList = string.Join(", ", createObjectOptions.Constructor.Parameters.Select(x => x.Type.ToString()));

                        sbDelegateVars.AppendLine($"private static Func<{paramList}, {classOrStruct.Name}> _{classOrStruct.Name}_CreateDelegate = GetCreateDelegate<{paramList}, {classOrStruct.Name}>();");
                    }

                    sbObjectCreation.Append($"var {createObjectOptions.NewObjVarName} = _{classOrStruct.Name}_CreateDelegate(");

                }
                else
                {
                    sbObjectCreation.Append($"//Skipped illegal creation of object: '{classOrStruct.Name}', due to accesssibility of: '{classOrStruct.DeclaredAccessibility}' and constructor accessibility of: '{createObjectOptions.Constructor.DeclaredAccessibility}'. " +
                        $"Set '{createObjectOptions.AllowIllegalObjCreation}' to use compiled delegates.");
                }
            }
            else
            {
                sbObjectCreation.Append($"var {createObjectOptions.NewObjVarName} = new {classOrStruct.Name}(");
            }

            for (int i = 0; i < createObjectOptions.ConstructorArgValues.Count(); i++)
            {
                var arg = createObjectOptions.ConstructorArgValues.ElementAt(i);

                if (i == 0)
                    sbObjectCreation.Append($"{arg}");
                else
                    sbObjectCreation.Append($", {arg}");
            }

            var initParams = createObjectOptions.PropertyValues.Where(x => canAccessObj && x.PropertySymbol.SetMethod.IsInitOnly && x.PropertySymbol.SetMethod.CanAccessFrom(createObjectOptions.AssemblyToCreateIn));

            if (initParams.Count() > 0)
            {
                sbObjectCreation.AppendLine(")\r\n{");

                foreach (var propValue in initParams)
                {
                    sbObjectCreation.AppendLine($"\t{propValue.PropertySymbol.Name} = {propValue.Value},");

                }

                sbObjectCreation.AppendLine("};\r\n");
            }
            else
            {
                sbObjectCreation.AppendLine($");\r\n");
            }

            bool createdSetDelegate = false;

            foreach (var propValue in createObjectOptions.PropertyValues.Where(x => !initParams.Contains(x)))
            {
                if (propValue.PropertySymbol.SetMethod.IsInitOnly || !propValue.PropertySymbol.SetMethod.CanAccessFrom(createObjectOptions.AssemblyToCreateIn))
                {
                    if (propValue.AllowIllegalSet)
                    {
                        var setDelegate = $"_{classOrStruct.Name}_{propValue.PropertySymbol.Name}_SetDelegate";

                        sbDelegateVars.AppendLine($"private static Action<{classOrStruct.Name}, {propValue.PropertySymbol.Type}> {setDelegate} = GetSetDelegate(({classOrStruct.Name} x) => x.{propValue.PropertySymbol.Name});");

                        sbObjectCreation.AppendLine($"{setDelegate}({createObjectOptions.NewObjVarName}, {propValue.Value});");

                        if (!createdSetDelegate)
                        {
                            sbDelegateCreateMethods.AppendLine(GetDelegateSetMethod());
                            createdSetDelegate = true;
                        }
                    }
                    else
                    {
                        sbObjectCreation.AppendLine($"//Skipped illegal set for property: '{propValue.PropertySymbol.Name}', due to accesssibility of: '{propValue.PropertySymbol.SetMethod.DeclaredAccessibility}'. Set {nameof(propValue.AllowIllegalSet)} to use compiled delegates.");
                    }
                }
                else
                {
                    sbObjectCreation.AppendLine($"{createObjectOptions.NewObjVarName}.{propValue.PropertySymbol.Name} = {propValue.Value};");
                }
            }

            staticDelegateMethods = sbDelegateCreateMethods.ToString();
            staticDelegateVars = sbDelegateVars.ToString();

            return sbObjectCreation.ToString();

            string GetDelegateCreateMethod(ImmutableArray<IParameterSymbol> parameters)
            {
                if (parameters.Length == 0)
                {
                    return $@"
public static Func<T> GetCreateDelegate<T>()
{{
    var constructor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null);
    var creatorExpression = Expression.Lambda<Func<T>>(Expression.New(constructor));

    return creatorExpression.Compile();
}}
";
                }

                return $@"
public static Func<{GetParamTypeList()}, T> GetCreateDelegate<{GetParamTypeList()}, T>()
{{
    var constructor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, new Type[] {{ {GetParamTypeList(true)} }});

{GetParamList()}

    var creatorExpression = Expression.Lambda<Func<{GetParamTypeList()}, T>>(Expression.New(constructor, new Expression[] {{ {GetExpressionParamList()} }}), {GetExpressionParamList()});

    return creatorExpression.Compile();
}}
";

                string GetParamTypeList(bool isTypeOf = false)
                {
                    var paramTypeList = new StringBuilder();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i == 0)
                        {
                            if (isTypeOf)
                                paramTypeList.Append($"typeof(P{i + 1})");
                            else
                                paramTypeList.Append($"P{i + 1}");
                        }
                        else
                        {
                            if (isTypeOf)
                                paramTypeList.Append($", typeof(P{i + 1})");
                            else
                                paramTypeList.Append($", P{i + 1}");
                        }

                    }

                    return paramTypeList.ToString();
                }

                string GetParamList()
                {
                    var paramList = new StringBuilder();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramList.AppendLine($"    var parameter{i + 1} = Expression.Parameter(typeof(P{i + 1}), \"p{i + 1}\");");
                    }

                    return paramList.ToString();
                }

                string GetExpressionParamList()
                {
                    var paramList = new StringBuilder();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramList.Append($"parameter{i + 1}");
                    }

                    return paramList.ToString();
                }
            }

            string GetDelegateSetMethod()
            {

                return @"
private static Action<M, R> GetSetDelegate<M, R>(Expression<Func<M, R>> propExp)
{
    if (propExp.Body.NodeType != ExpressionType.MemberAccess)
        throw new ArgumentException(""This should be a property member"", nameof(propExp));

    //Input model
    var model = propExp.Parameters[0];
    //Input value to set
    var valueToSet = Expression.Variable(typeof(R), ""v"");
    //Member access
    var expressionMember = propExp.Body;

    //We wrap the action into a lambda expression with parameters
    return Expression.Lambda<Action<M, R>>(Expression.Assign(expressionMember, valueToSet), model, valueToSet).Compile();
}";
            }
        }

        public static INamedTypeSymbol GetUnderlyingType(this INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return (INamedTypeSymbol)typeSymbol.TypeArguments[0];
            }
            else
            {
                return typeSymbol;
            }
        }

        public static bool IsPrimitive(this ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_Byte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Char:
                case SpecialType.System_String:
                case SpecialType.System_Object:
                    return true;
                default:
                    return false;
            }
        }

        public static string ToVarName(this ISymbol symbol, bool lowerFirstChar = true, string prefix = "", string suffix = "")
        {

            if (lowerFirstChar)
            {
                var firstChar = char.ToLowerInvariant(symbol.Name[0]);

                return $"{prefix}{firstChar}{symbol.Name.Substring(1)}{suffix}";
            }

            return $"{prefix}{symbol.Name}{suffix}";
        }

        /// <summary>
        /// Determines if a type can be created from a provided assembly.
        /// </summary>
        /// <param name="fromAssembly">The assembly in which you want to create the type</param>
        /// <param name="symbolToCheck">The type symbol you want to check for accessability</param>
        /// <param name="includeConstructors">If true, will check if the type has any constructors that can be accessed</param>
        /// <returns>true if the type can be created from the assembly provided, otherwise false</returns>
        public static bool CanAccess(this IAssemblySymbol fromAssembly, ITypeSymbol symbolToCheck, bool includeConstructors = true)
        {
            return IsAccessible(fromAssembly, symbolToCheck, includeConstructors);
        }

        /// <summary>
        /// Determines if a method can be invoked from a provided assembly.
        /// </summary>
        /// <param name="fromAssembly">The assembly in which you want to invoke the method</param>
        /// <param name="symbolToCheck">The method symbol you want to check for accessability</param>
        /// <returns>true if the method can be invoked from the assembly provided, otherwise false</returns>
        public static bool CanAccess(this IAssemblySymbol fromAssembly, IMethodSymbol symbolToCheck)
        {
            return IsAccessible(fromAssembly, symbolToCheck);
        }


        /// <summary>
        /// Determines if a type can be created from a provided assembly.
        /// </summary>
        /// <param name="symbolToCheck">The type symbol you want to check for accessability</param>
        /// <param name="fromAssembly">The assembly in which you want to create the type</param>
        /// <param name="includeConstructors">If true, will check if the type has any constructors that can be accessed</param>
        /// <returns>true if the type can be created from the assembly provided, otherwise false</returns>
        public static bool CanAccessFrom(this ITypeSymbol symbolToCheck, IAssemblySymbol fromAssembly, bool includeConstructors = true)
        {
            return IsAccessible(fromAssembly, symbolToCheck, includeConstructors);
        }

        /// <summary>
        /// Determines if a method can be invoked from an assembly.
        /// </summary>
        /// <param name="symbolToCheck">The symbol you want to check for accessability</param>
        /// <param name="fromAssembly">The assembly in which you want to onvoke the method</param>
        /// <returns>true if the method can be invoked from the assembly provided, otherwise false</returns>
        public static bool CanAccessFrom(this IMethodSymbol symbolToCheck, IAssemblySymbol fromAssembly)
        {
            return IsAccessible(fromAssembly, symbolToCheck);
        }

        private static bool IsAccessible(IAssemblySymbol fromAssembly, ISymbol typeToCheck, bool includeConstructors = false)
        {

            if (typeToCheck.DeclaredAccessibility == Accessibility.Public ||
                (typeToCheck.DeclaredAccessibility == Accessibility.Internal &&
                 (fromAssembly.Equals(typeToCheck.ContainingAssembly, SymbolEqualityComparer.Default) ||
                    IsAccessibleWithInternalsVisibleTo())))
            {
                if (includeConstructors && typeToCheck is INamedTypeSymbol namedTypeSymbol)
                {
                    return HasAccessibleConstructor(namedTypeSymbol);
                }

                if (typeToCheck is IMethodSymbol methodSymbol)
                {
                    foreach (var param in methodSymbol.Parameters)
                    {
                        if (!IsAccessible(fromAssembly, param.Type, includeConstructors))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            return false;

            bool HasAccessibleConstructor(INamedTypeSymbol namedTypeSymbol)
            {
                var constructors = namedTypeSymbol.Constructors.Where(x => !x.IsStatic);

                if (constructors.Count() == 0)
                {
                    return false;
                }

                foreach (var ctor in constructors)
                {
                    if (ctor.DeclaredAccessibility == Accessibility.Public ||
                        (ctor.DeclaredAccessibility == Accessibility.Internal &&
                         (fromAssembly.Equals(ctor.ContainingAssembly, SymbolEqualityComparer.Default) ||
                            IsAccessibleWithInternalsVisibleTo())))
                    {
                        return true;
                    }

                }

                return false;
            }

            bool IsAccessibleWithInternalsVisibleTo()
            {
                var fromAttributes = fromAssembly.GetAttributes();

                foreach (var attribute in fromAttributes)
                {
                    if (attribute.AttributeClass.Name == "InternalsVisibleToAttribute")
                    {
                        var assemblyName = attribute.ConstructorArguments.FirstOrDefault().Value.ToString();
                        if (assemblyName == typeToCheck.ContainingAssembly.Name)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

    }

}
