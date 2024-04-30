using ByteSocket.SourceGenHelpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using ByteSocket.RocketCsv.SourceGenerator.Shared;

namespace ByteSocket.RocketCsv.SourceGenerator
{
    public class CsvMapClassInfo
    {

        public CsvMapClassInfo(SemanticModel semanticModel, ClassDeclarationSyntax classSyntax)
        {
            var diagnosticList = new List<Diagnostic>();

            ClassSyntax = classSyntax;
            ClassSymbol = semanticModel.GetDeclaredSymbol(classSyntax);
            Diagnostics = diagnosticList;

            var isPartial = classSyntax.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword));
            var hasCsvBase = ClassSymbol.BaseType.Name.StartsWith("CsvMapBase");

            if (!isPartial)
            {
                diagnosticList.Add(Diagnostic.Create("CSV001", "", "Class must be partial", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, location: classSyntax.GetLocation()));
            }

            if (!hasCsvBase)
            {
                diagnosticList.Add(Diagnostic.Create("CSV002", "", "Class must inherit from CsvMapBase", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, location: classSyntax.GetLocation()));
            }

            //Cut off due to possible errors
            if (diagnosticList.Any())
            {
                return;
            }

            CsvType = ClassSymbol.BaseType.TypeArguments.FirstOrDefault() as INamedTypeSymbol;

            var configureMethod = classSyntax.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(x => x.Identifier.Text == "Configure");

            if (configureMethod == null)
            {
                diagnosticList.Add(Diagnostic.Create("CSV003", "", "This class must have a method called 'Configure'", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, location: classSyntax.GetLocation()));

                return;
            }
            else
            {
                var configureMethodSymbol = semanticModel.GetDeclaredSymbol(configureMethod);

                if (configureMethodSymbol.Parameters.Length != 1 || configureMethodSymbol.Parameters[0].Type.Equals(CsvType, SymbolEqualityComparer.IncludeNullability))
                {
                    diagnosticList.Add(Diagnostic.Create("CSV004", "", $"Configure method must have a single parameter of type '{nameof(ICsvMapBuilder<object>)}<{CsvType.Name}>'", DiagnosticSeverity.Error, DiagnosticSeverity.Error, true, 0, location: configureMethod.GetLocation()));

                    return;
                }
            }

            var fluentMethodGroups = FluentApiHelper.GetFluentApiMethods(semanticModel, configureMethod, out var fluentErrors);

            if (fluentErrors.Any())
            {
                diagnosticList.AddRange(fluentErrors);

                return;
            }

            CsvMapBuilderOptions = CsvMapBuilderOptions.Create(CsvType, fluentMethodGroups, out var csvOptionErrors);

            if (csvOptionErrors.Any())
            {
                diagnosticList.AddRange(csvOptionErrors);

                return;
            }
        }

        public ClassDeclarationSyntax ClassSyntax { get; }
        public INamedTypeSymbol ClassSymbol { get; }
        public INamedTypeSymbol CsvType { get; }
        public CsvMapBuilderOptions CsvMapBuilderOptions { get; }
        public IEnumerable<Diagnostic> Diagnostics { get; }


        //public override bool Equals(object obj)
        //{
        //    if (obj is CsvMapClassInfo csvMapClassInfo)
        //    {
        //        if (!csvMapClassInfo.ClassSyntax.Equals(this.ClassSyntax))
        //            return false;

        //        if (!csvMapClassInfo.ClassSymbol.Equals(this.ClassSymbol, SymbolEqualityComparer.IncludeNullability))
        //            return false;

        //        if (!csvMapClassInfo.CsvType.Equals(this.CsvType, SymbolEqualityComparer.IncludeNullability))
        //            return false;

        //        if (!csvMapClassInfo.BestCsvTypeConstructor.Equals(this.BestCsvTypeConstructor, SymbolEqualityComparer.Default))
        //            return false;

        //        if (!csvMapClassInfo.BestCsvTypeConstructorMatchingProperties.SequenceEqual(this.BestCsvTypeConstructorMatchingProperties, SymbolEqualityComparer.Default))
        //            return false;

        //        if (!csvMapClassInfo.Diagnostics.SequenceEqual(this.Diagnostics))
        //            return false;
        //    }

        //    return false;
        //}
    }

}
