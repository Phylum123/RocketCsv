using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ByteSocket.SourceGenHelpers
{
    public static class FluentApiHelper
    {
        public static IEnumerable<FluentMethodGroup> GetFluentApiMethods(SemanticModel semanticModel, MethodDeclarationSyntax methodSyntax, out IEnumerable<Diagnostic> errors, int maxMethodGroups = int.MaxValue)
        {
            var fluentMethodGroups = new List<FluentMethodGroup>();

            var errorList = new List<Diagnostic>();
            errors = errorList;


            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);

            if (methodSymbol == null)
            {
                errorList.Add(CreateMethodErrorDiag(methodSyntax.Identifier.ValueText, methodSyntax.GetLocation()));

                return fluentMethodGroups;
            }

            var fluentParamNames = methodSyntax.ParameterList.Parameters.Select(x => x.Identifier.ValueText);

            //Search the provided method for use of the method parameter
            foreach (var expressionStatement in methodSyntax.Body.Statements.OfType<ExpressionStatementSyntax>())
            {
                //See if the method parameter is being used, which would mean this expression statement is using the fluent API.
                var paramIdentifier = expressionStatement.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault(x => fluentParamNames.Contains(x.Identifier.ValueText));

                if (paramIdentifier != null)
                {
                    var currentMethodGroup = new FluentMethodGroup();
                    var currentMethodList = new List<FluentMethodInfo>();

                    //Methods are given in reverse order when they are chained via a fluent API. Putting them in a stack, we will pop them out in the proper order
                    var fluentMethodSyntaxStack = new Stack<InvocationExpressionSyntax>(expressionStatement.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => !x.Ancestors().Any(y => y is LambdaExpressionSyntax)));

                    //Loop through the fluent API method calls
                    while (fluentMethodSyntaxStack.Count > 0)
                    {
                        var fluentInvocationSyntax = fluentMethodSyntaxStack.Pop();
                        var fluentMethodSymbol = semanticModel.GetSymbolInfo(fluentInvocationSyntax.Expression).Symbol as IMethodSymbol;

                        var argumentValues = new List<object>();

                        //Loop through the parameters of the method and get the values of the arguments
                        for (int paramNum = 0; paramNum < fluentMethodSymbol.Parameters.Count(); paramNum++)
                        {
                            var paramSymbol = fluentMethodSymbol.Parameters[paramNum];
                            var paramTypeSymbol = fluentMethodSymbol.Parameters[paramNum] as INamedTypeSymbol;

                            if (paramNum < fluentInvocationSyntax.ArgumentList.Arguments.Count)
                            {
                                var argumentSyntax = fluentInvocationSyntax.ArgumentList.Arguments[paramNum];

                                errorList.AddRange(FluentApiExpressionHelper.GetFluentConstantValue(semanticModel, paramSymbol, argumentSyntax, out var constValue));

                                argumentValues.Add(constValue);
                            }
                            //Default Parameter
                            else
                            {
                                if (paramSymbol.HasExplicitDefaultValue)
                                {
                                    argumentValues.Add(paramSymbol.ExplicitDefaultValue);
                                }
                                else
                                {
                                    errorList.Add(CreateMissingParameterValueDiag(fluentMethodSymbol.Name, paramSymbol.Name, fluentInvocationSyntax.GetLocation()));
                                }
                            }

                        }

                        currentMethodList.Add(new FluentMethodInfo(fluentInvocationSyntax, fluentMethodSymbol, argumentValues));
                    }

                    if (currentMethodList.Count > 0)
                    {
                        currentMethodGroup.FluentMethods.AddRange(currentMethodList);
                    }

                    fluentMethodGroups.Add(currentMethodGroup);
                }
                else
                {
                    var methodSymbolTypes = methodSymbol.Parameters.Select(x => x.Type);
                    var methodCallSyntax = expressionStatement.ChildNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    var methodCallSymbol = semanticModel.GetSymbolInfo(methodCallSyntax.Expression).Symbol as IMethodSymbol;

                    if (methodCallSymbol.DeclaringSyntaxReferences.Length > 0 && methodCallSymbol.ReturnsVoid && !methodCallSymbol.IsPartialDefinition && 
                        methodCallSymbol.Parameters.All(x => methodSymbolTypes.Contains(x.Type, SymbolEqualityComparer.Default)))
                    {
                        var resultMethodGroups = GetFluentApiMethods(semanticModel, (MethodDeclarationSyntax)methodCallSymbol.DeclaringSyntaxReferences[0].GetSyntax(), out var diagnostics);

                        fluentMethodGroups.AddRange(resultMethodGroups);
                        errorList.AddRange(diagnostics);

                    }
                    else
                    {
                        errorList.Add(CreateInvalidMethodCallDiag(methodCallSymbol.Name, methodCallSyntax.Expression.GetLocation()));
                    }
                }

            }

            return fluentMethodGroups;
        }

        private static Diagnostic CreateMethodErrorDiag(string methodName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH001", "Method Error", "Method '{0}' could not be resolved.", "",
                                     DiagnosticSeverity.Error, true), location, methodName);

        }
        private static Diagnostic CreateMissingParameterValueDiag(string methodName, string parameterName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH002", "Invalid Method Call", "Method '{0}' requires a value for parameter '{1}'",
                "", DiagnosticSeverity.Error, true), location, methodName, parameterName);


        }

        private static Diagnostic CreateInvalidMethodCallDiag(string methodName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("FAH003", "Invalid Method Call",
                "Method '{0}' cannot be called. Non fluent api methods are restricted to methods that we have source code access to, returns void, marked partial and only has parameters that the hotsing method has.",
                "", DiagnosticSeverity.Error, true), location, methodName);

        }
    }
}