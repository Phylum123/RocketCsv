using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace ByteSocket.SourceGenHelpers
{
    public static class FluentApiExpressionHelper
    {
        public static IEnumerable<Diagnostic> GetFluentConstantValue(SemanticModel semanticModel, IParameterSymbol parameterSymbol, ArgumentSyntax argumentSyntax, out object constantValue)
        {
            constantValue = default;
            var errors = new List<Diagnostic>();

            var arrayValueExp = argumentSyntax.DescendantNodes().Where(x => x is InitializerExpressionSyntax || x is CollectionExpressionSyntax).FirstOrDefault();
            var namedTypeSymbol = parameterSymbol.Type as INamedTypeSymbol;


            //Property expressions
            if (namedTypeSymbol is INamedTypeSymbol { IsGenericType: true, Name: "Expression", TypeArguments.Length: 1 } && 
                namedTypeSymbol.TypeArguments[0] is INamedTypeSymbol funcTypeArgument &&
                funcTypeArgument is INamedTypeSymbol { IsGenericType: true, Name: "Func", TypeArguments.Length: 2 })
            {
                var mainType = funcTypeArgument.TypeArguments[0] as INamedTypeSymbol;
                var singleProp = argumentSyntax.Expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();

                var property = singleProp == default ? default : mainType.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(x => x.Name == singleProp?.Name.ToString());

                if (property != default)
                {
                    constantValue = property;
                }
                else
                {
                    errors.Add(CreateMissingPropertyDiag(singleProp?.Name.ToString(), argumentSyntax.GetLocation()));
                }

            }
            //Delegates
            else if (namedTypeSymbol != default && namedTypeSymbol.TypeKind == TypeKind.Delegate)
            {
                if (argumentSyntax.Expression is LambdaExpressionSyntax lambdaExp)
                {
                    constantValue = semanticModel.GetSymbolInfo(lambdaExp).Symbol as IMethodSymbol;
                }
                else if (argumentSyntax.Expression is IdentifierNameSyntax idName)
                {
                    constantValue = semanticModel.GetSymbolInfo(idName).Symbol as IMethodSymbol;
                }
                else
                {

                }
            }
            // { "Test", "Hello" }
            else if (arrayValueExp is InitializerExpressionSyntax arrayInit)
            {
                var paramType = parameterSymbol.Type as IArrayTypeSymbol;

                if (!IsValidType(paramType))
                {
                    errors.Add(CreateNonPrimitiveDiag(argumentSyntax.GetLocation()));
                }
                else
                {
                    var arrayValues = GetArrayConstInit(semanticModel, arrayInit, out var errs);

                    if (errs.Any())
                    {
                        errors.AddRange(errs);
                    }
                    else
                    {
                        constantValue = arrayValues;
                    }
                }

            }
            // ["Test, "Hello"]
            else if (arrayValueExp is CollectionExpressionSyntax collExp)
            {
                var paramType = parameterSymbol.Type as IArrayTypeSymbol;

                if (!IsValidType(paramType))
                {
                    errors.Add(CreateNonPrimitiveDiag(argumentSyntax.GetLocation()));
                }
                else
                {
                    var arrayValues = GetArrayConstColl(semanticModel, collExp, out var errs);

                    if (errs.Any())
                    {
                        errors.AddRange(errs);
                    }
                    else
                    {
                        constantValue = arrayValues;
                    }
                }

            }
            else
            {
                //Get the constant/literal value of the argument
                var constValue = semanticModel.GetConstantValue(argumentSyntax.Expression);

                if (constValue.HasValue)
                {
                    constantValue = constValue.Value;
                }
                else
                {
                    errors.Add(CreateNonConstantOrLiteralDiag(argumentSyntax.GetLocation()));
                }
            }

            return errors;

            bool IsValidType(ITypeSymbol typeSymbol)
            {
                return typeSymbol.IsPrimitive() || typeSymbol.SpecialType == SpecialType.System_Enum;
            }
        }

        private static object[] GetArrayConstInit(SemanticModel semanticModel, InitializerExpressionSyntax iSyntax, out IEnumerable<Diagnostic> errors)
        {
            var errorList = new List<Diagnostic>();
            errors = errorList;

            var array = new object[iSyntax.Expressions.Count];

            for (int i = 0; i < iSyntax.Expressions.Count; i++)
            {
                var constValue = semanticModel.GetConstantValue(iSyntax.Expressions[i]);

                if (constValue.HasValue)
                {
                    array[i] = constValue.Value;
                }
                else
                {
                    errorList.Add(CreateNonConstantOrLiteralDiag(iSyntax.Expressions[i].GetLocation()));
                }
            }

            return array;
        }

        private static object[] GetArrayConstColl(SemanticModel semanticModel, CollectionExpressionSyntax cSyntax, out IEnumerable<Diagnostic> errors)
        {
            var errorList = new List<Diagnostic>();
            errors = errorList;

            var array = new object[cSyntax.Elements.Count];

            for (int i = 0; i < cSyntax.Elements.Count; i++)
            {
                var exp = cSyntax.Elements[i].ChildNodes().OfType<ExpressionSyntax>().FirstOrDefault();

                var constValue = semanticModel.GetConstantValue(exp);

                if (constValue.HasValue)
                {
                    array[i] = constValue.Value;
                }
                else
                {
                    errorList.Add(CreateNonConstantOrLiteralDiag(cSyntax.Elements[i].GetLocation()));
                }
            }

            return array;
        }

        #region Diagnostics

        private static Diagnostic CreateNonConstantOrLiteralDiag(Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH010", "Non constant or literal", "Values may only be constants or lieterals, with the excpetion of property expressions, delegates and arrays created inline, with an initializer.", "",
                                     DiagnosticSeverity.Error, true), location);

        }
        private static Diagnostic CreateMissingPropertyDiag(string propertyName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH011", "Cannot find property", "Cannot find property '{0}'", "",
                                     DiagnosticSeverity.Error, true), location, propertyName);

        }

        private static Diagnostic CreateNonPrimitiveDiag(Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH012", "Only Primitives and Enums Supported", "Only primitive element types and enums are supported.", "",
                                     DiagnosticSeverity.Error, true), location);

        }
        private static Diagnostic CreateUnkownArrayInitDiag(Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH013", "Unknown Array Init", "Unknown array initialization syntax!", "",
                                     DiagnosticSeverity.Error, true), location);

        }

        #endregion
    }
}