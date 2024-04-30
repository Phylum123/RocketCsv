using ByteSocket.RocketCsv.SourceGenerator.Shared;
using ByteSocket.SourceGenHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ByteSocket.RocketCsv.SourceGenerator
{
    public class MappingInfo
    {
        public bool AutoMapped { get; set; } = false;
        public string CsvColumnName { get; set; } = "";
        public int CsvColumnIndex { get; set; } = -1;
        public ParseFailure ParseFailure { get; set; } = ParseFailure.UseDefaultParseFailure;
        public object SetToValue { get; set; } = null;
        public IMethodSymbol CustomParseMethod { get; set; } = null;
    }

    public record CsvMapBuilderOptions
    {


        private CsvMapBuilderOptions()
        {
        }

        public INamedTypeSymbol CsvMappingType { get; set; }
        public string RowDelimiter { get; set; } = "\n";
        public string HeaderDelimiter { get; set; }
        public char[] HeaderTrimChars { get; set; }
        public char[] HeaderRemoveChars { get; set; }
        public StringComparison HeaderStringComparison { get; set; } = StringComparison.Ordinal;
        public string ColumnDelimiter { get; set; } = ",";
        public char[] ColumnTrimChars { get; set; } = [' ', '\t', '\r'];
        public bool AllowTooFewColumns { get; set; } = false;
        public char StringDelimiter { get; set; } = '"';
        public char StringDelimiterEscape { get; set; } = '\\';
        public bool TrimStringDelimiter { get; set; } = true;
        public StringTrim StringTrimOption { get; set; } = StringTrim.TrimUntilStringDelimiter;
        public IMethodSymbol ChosenConstructor { get; set; } = null;
        public IEnumerable<IPropertySymbol> MatchingConstructorProperties { get; set; }

        public OrderedDictionary<IPropertySymbol, MappingInfo> ColumnIndexMap { get; } = new OrderedDictionary<IPropertySymbol, MappingInfo>(SymbolEqualityComparer.Default);
        public OrderedDictionary<IPropertySymbol, MappingInfo> ColumnNameMap { get; } = new OrderedDictionary<IPropertySymbol, MappingInfo>(SymbolEqualityComparer.Default);


        public static CsvMapBuilderOptions Create(INamedTypeSymbol mappingType, IEnumerable<FluentMethodGroup> fluentMethodGroups, out IEnumerable<Diagnostic> errors)
        {

            var csvOptions = new CsvMapBuilderOptions();
            var errorList = new List<Diagnostic>();
            errors = errorList;

            csvOptions.CsvMappingType = mappingType;

            var parseFailureDefault = ParseFailure.ThrowException;

            foreach (var group in fluentMethodGroups)
            {
                for (int methodPos = 0; methodPos < group.FluentMethods.Count; methodPos++)
                {
                    var methodInfo = group.FluentMethods[methodPos];
                    var argValue = methodInfo.ArgumentValues.FirstOrDefault();

                    if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.RowDelimiter))
                    {
                        csvOptions.RowDelimiter = argValue.ToString();
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.HeaderDelimiter))
                    {
                        csvOptions.HeaderDelimiter = argValue.ToString();
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.ColumnDelimiter))
                    {
                        csvOptions.ColumnDelimiter = argValue.ToString();
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.ColumnTrimChars))
                    {

                        if (argValue == null)
                            continue;

                        csvOptions.ColumnTrimChars = Array.ConvertAll(argValue as object[], Convert.ToChar);
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.AllowTooFewColumns))
                    {
                        csvOptions.AllowTooFewColumns = true;
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.StringDelimiter))
                    {
                        csvOptions.StringDelimiter = (char)argValue;
                        csvOptions.StringDelimiterEscape = (char)methodInfo.ArgumentValues.ElementAt(1);
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.StringTrimOptions))
                    {
                        csvOptions.TrimStringDelimiter = (bool)argValue;
                        csvOptions.StringTrimOption = (StringTrim)methodInfo.ArgumentValues.ElementAt(1);

                        var arg2Value = methodInfo.ArgumentValues.ElementAt(2);

                        if (argValue == null)
                            continue;
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.DefaultParseFailureBehavior))
                    {
                        parseFailureDefault = (ParseFailure)argValue;
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.ChooseEmptyConstructor))
                    {
                        var emptyCtor = mappingType.Constructors.FirstOrDefault(x => x.Parameters.Length == 0);

                        if (emptyCtor == null)
                        {
                            errorList.Add(Diagnostic_NoChosenConstructor(mappingType.Name, methodInfo.FluentMethodSyntax.GetLocation()));
                        }
                        else
                        {
                            csvOptions.ChosenConstructor = emptyCtor;
                            csvOptions.MatchingConstructorProperties = null;
                        }
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.ChooseConstructor))
                    {
                        var matchingProps = new List<IPropertySymbol>();
                        var mappingProps = mappingType.GetMembers().OfType<IPropertySymbol>();

                        bool foundConstructor = false;

                        foreach (var ctor in mappingType.Constructors)
                        {

                            if (ctor.Parameters.Length == methodInfo.FluentMethodSymbol.TypeParameters.Length)
                            {
                                bool found = true;

                                for (int i = 0; i < ctor.Parameters.Length; i++)
                                {
                                    var matchingProp = mappingProps.FirstOrDefault(x => x.Name.Equals(ctor.Parameters[i].Name, StringComparison.OrdinalIgnoreCase));
                                    var genericTypeSymbol = methodInfo.FluentMethodSymbol.TypeArguments[i];

                                    if (matchingProp != null && (genericTypeSymbol?.Equals(ctor.Parameters[i].Type, SymbolEqualityComparer.Default) ?? false))
                                    {
                                        matchingProps.Add(matchingProp);
                                    }
                                    else
                                    {
                                        matchingProps.Clear();

                                        found = false;
                                        break;
                                    }
                                }

                                if (found)
                                {
                                    foundConstructor = true;
                                    csvOptions.ChosenConstructor = ctor;
                                    csvOptions.MatchingConstructorProperties = matchingProps;
                                    break;
                                }
                            }
                        }

                        if (!foundConstructor)
                        {
                            errorList.Add(Diagnostic_NoChosenConstructor(mappingType.Name, methodInfo.FluentMethodSyntax.GetLocation()));
                        }

                    }
                    //One of these 2 or MapColumns() has to be called before any of the column mappings, this is enforced by the fluent API
                    //----------------------------------------------------------------------------------------------------------------------------------------------------
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.AutoMapByIndex))
                    {
                        var publicProperties = mappingType.GetMembers().OfType<IPropertySymbol>().Where(x => x.DeclaredAccessibility == Accessibility.Public);

                        for (int i = 0; i < publicProperties.Count(); i++)
                        {
                            csvOptions.ColumnIndexMap.Add(publicProperties.ElementAt(i), new MappingInfo() { AutoMapped = true, CsvColumnIndex = i });
                        }
                    }
                    else if (methodInfo.FluentMethodSymbol.Name == nameof(ICsvMapBuilder<object>.AutoMapByName))
                    {
                        csvOptions.HeaderStringComparison = (StringComparison)argValue;

                        var headerRemoveChars = methodInfo.ArgumentValues.ElementAt(1) as object[];

                        if (headerRemoveChars != null)
                            csvOptions.HeaderRemoveChars = Array.ConvertAll(headerRemoveChars, Convert.ToChar);
                        else
                            csvOptions.HeaderRemoveChars = [' ', '\t'];

                        var publicProperties = mappingType.GetMembers().OfType<IPropertySymbol>().Where(x => x.DeclaredAccessibility == Accessibility.Public && x.SetMethod != null && x.SetMethod.DeclaredAccessibility == Accessibility.Public);

                        for (int i = 0; i < publicProperties.Count(); i++)
                        {
                            csvOptions.ColumnNameMap.Add(publicProperties.ElementAt(i), new MappingInfo() { AutoMapped = true, CsvColumnName = publicProperties.ElementAt(i).Name, ParseFailure = parseFailureDefault });
                        }
                    }
                    //----------------------------------------------------------------------------------------------------------------------------------------------------

                    else if (methodInfo.FluentMethodSymbol.Name == nameof(IMapToColumnBuilder<object>.MapToColumn))
                    {
                        var propSymbol = argValue as IPropertySymbol;

                        csvOptions.ColumnIndexMap.TryGetValue(propSymbol, out var indexInfo);
                        csvOptions.ColumnNameMap.TryGetValue(propSymbol, out var nameInfo);

                        if ((indexInfo != null && indexInfo.AutoMapped) ||
                            (nameInfo != null && nameInfo.AutoMapped) ||
                            (indexInfo == null && nameInfo == null))
                        {
                            if (indexInfo != null)
                                csvOptions.ColumnIndexMap.Remove(propSymbol);

                            if (nameInfo != null)
                                csvOptions.ColumnNameMap.Remove(propSymbol);

                            CreateMapping(propSymbol);
                        }
                        else
                        {
                            errorList.Add(Diagnostic_DuplicateColumnMappingDiag(propSymbol.Name, methodInfo.FluentMethodSyntax.GetLocation()));
                        }

                    }

                    void CreateMapping(IPropertySymbol propSymbol)
                    {
                        var mappingInfo = new MappingInfo();
                        mappingInfo.ParseFailure = parseFailureDefault;

                        //Check for modifying methods that are chained after the MapToColumn/Override method
                        for (int i = methodPos + 1; i < group.FluentMethods.Count; i++)
                        {
                            var nextMethod = group.FluentMethods[i];

                            if (nextMethod.FluentMethodSymbol.Name == nameof(IMapToColumnOptionsBuilder<object, object>.OnParseFailure))
                            {
                                mappingInfo.ParseFailure = (ParseFailure)nextMethod.ArgumentValues.ElementAt(0);
                                mappingInfo.SetToValue = nextMethod.ArgumentValues.ElementAt(1);
                            }
                            else if (nextMethod.FluentMethodSymbol.Name == nameof(IMapToColumnOptionsBuilder<object, object>.CustomParse))
                            {
                                if (nextMethod.ArgumentValues.ElementAt(0) is IMethodSymbol methodSymbol)
                                {
                                    mappingInfo.CustomParseMethod = methodSymbol;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        //Check if it is the index or name version of the column mapping
                        if (methodInfo.FluentMethodSymbol.Parameters[1].Type.SpecialType == SpecialType.System_Int32)
                        {
                            mappingInfo.CsvColumnIndex = (int)methodInfo.ArgumentValues.ElementAt(1);

                            csvOptions.ColumnIndexMap.Add(propSymbol, mappingInfo);
                        }
                        else
                        {
                            mappingInfo.CsvColumnName = methodInfo.ArgumentValues.ElementAt(1) as string;

                            if (string.IsNullOrWhiteSpace(mappingInfo.CsvColumnName))
                            {
                                errorList.Add(Diagnostic_EmptyCsvColumnName(propSymbol.Name, methodInfo.FluentMethodSyntax.GetLocation()));

                                return;
                            }

                            csvOptions.ColumnNameMap.Add(propSymbol, mappingInfo);
                        }
                    }
                }


                if (csvOptions.HeaderDelimiter == null)
                    csvOptions.HeaderDelimiter = csvOptions.ColumnDelimiter;

                if (csvOptions.HeaderTrimChars == null)
                    csvOptions.HeaderTrimChars = csvOptions.ColumnTrimChars;

                if (csvOptions.ColumnIndexMap.Count == 0 && csvOptions.ColumnNameMap.Count == 0 && fluentMethodGroups.Count() > 0)
                {
                    errorList.Add(Diagnostic_NoColumnMappings(fluentMethodGroups.ElementAt(0).FluentMethods.ElementAt(0).FluentMethodSyntax.GetLocation()));
                }

                if (csvOptions.ChosenConstructor == null)
                {
                    var accessability = new Accessibility[] { Accessibility.Public, Accessibility.Internal, Accessibility.Private, Accessibility.Protected };
                    var bestConstructor = mappingType.GetBestConstructor(SymbolEqualityComparer.Default, accessability, accessability);

                    if (bestConstructor == default)
                    {
                        errorList.Add(Diagnostic_NoConstructor(mappingType.Name, fluentMethodGroups.ElementAt(0).FluentMethods.ElementAt(0).FluentMethodSyntax.GetLocation()));
                    }
                    else
                    {
                        csvOptions.ChosenConstructor = bestConstructor.BestConstructor;
                        csvOptions.MatchingConstructorProperties = bestConstructor.MatchingProperties;
                    }

                }

            }

            return csvOptions;
        }

        private static Diagnostic Diagnostic_DuplicateColumnMappingDiag(string propertyName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("MAP001", "Invalid Mapping", "Mapping property '{0}' more than once is not allowed!", "",
                                     DiagnosticSeverity.Error, true), location, propertyName);

        }

        private static Diagnostic Diagnostic_EmptyCsvColumnName(string propertyName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("MAP002", "Invalid Mapping", "Mapping property '{0}' to an empty CSV column name is not allowed!", "",
                                     DiagnosticSeverity.Error, true), location, propertyName);

        }

        private static Diagnostic Diagnostic_NoColumnMappings(Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("MAP003", "Invalid Mapping", "You must map at least one csv column!", "",
                                     DiagnosticSeverity.Error, true), location);

        }

        private static Diagnostic Diagnostic_NoChosenConstructor(string mappingTypeName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("MAP004", "No Constructor", "Could not find chosen constructor for type {0}. Chosen constructors with paraemters, must have parameter names and types that match property names (ignoring case).", "",
                                     DiagnosticSeverity.Error, true), location, mappingTypeName);

        }
        private static Diagnostic Diagnostic_NoConstructor(string mappingTypeName, Location location)
        {
            return Diagnostic.Create(new DiagnosticDescriptor("MAP005", "No Constructor", "Could not find any suitable constructor for type {0}", "",
                                     DiagnosticSeverity.Error, true), location, mappingTypeName);

        }
    }
}
