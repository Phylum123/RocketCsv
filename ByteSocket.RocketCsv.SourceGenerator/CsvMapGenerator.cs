using ByteSocket.RocketCsv.SourceGenerator.Shared;
using ByteSocket.SourceGenHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ByteSocket.RocketCsv.SourceGenerator
{

    [Generator]
    public class CsvMapGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif

            // define the execution pipeline here via a series of transformations:


            var classInfoProvider = context.SyntaxProvider.ForAttributeWithMetadataName("ByteSocket.RocketCsv.SourceGenerator.Shared.CsvMapAttribute",
                (s, _) => s is ClassDeclarationSyntax,
                (ctx, _) =>
                {
                    var classSyntax = ctx.TargetNode as ClassDeclarationSyntax;

                    //Don't bother trying to generate the code if the syntax is in error.
                    if (classSyntax.ContainsDiagnostics && classSyntax.GetDiagnostics().Any(x => x.Severity == DiagnosticSeverity.Error))
                    {
                        return null;
                    }

                    return new CsvMapClassInfo(ctx.SemanticModel, classSyntax);
                }).WithTrackingName("CsvMapGeneratorInputTransform");

            context.RegisterImplementationSourceOutput(classInfoProvider, (spc, csvMapClassInfo) =>
            {
                var sourceWriter = new SourceWriter();

                //Stop if there are any errors
                if (csvMapClassInfo.Diagnostics.Any())
                {
                    foreach (var diag in csvMapClassInfo.Diagnostics)
                    {
                        spc.ReportDiagnostic(diag);
                    }

                    return;
                }

                WriteCsvMap(sourceWriter, csvMapClassInfo);
                spc.AddSource($"{csvMapClassInfo.ClassSymbol.Name}.g.cs", sourceWriter.ToSourceText());
            });

        }

        private static void WriteCsvMap(SourceWriter sourceWriter, CsvMapClassInfo csvMapClassInfo)
        {
            var namespaceStr = csvMapClassInfo.ClassSymbol.ContainingNamespace.ToDisplayString();
            var csvType = csvMapClassInfo.ClassSymbol.BaseType.TypeArguments.FirstOrDefault();
            var csvMapType = csvMapClassInfo.ClassSymbol.BaseType.TypeArguments[0] as INamedTypeSymbol;

            sourceWriter.WriteLine("using ByteSocket.RocketCsv.Core;");
            sourceWriter.WriteLine("using System;");
            sourceWriter.WriteLine("using System.Text;");
            sourceWriter.WriteLine("using System.Reflection;");
            sourceWriter.WriteLine("using System.Linq;");
            sourceWriter.WriteLine("using System.Linq.Expressions;\r\n");

            if (!string.IsNullOrEmpty(namespaceStr))
            {
                sourceWriter.OpenBlock($"namespace {namespaceStr}");
            }

            sourceWriter.OpenBlock($"partial class {csvMapClassInfo.ClassSymbol.Name}");
            sourceWriter.WriteLine("");
            sourceWriter.WriteLine($"public {csvMapClassInfo.ClassSymbol.Name}(CsvReader csvReader) : base(csvReader) {{ }}");
            sourceWriter.WriteLine("");

            var objectCreateOptions = GetCreateObjectOptions();
            var objectCreateStr = csvMapType.CreateObject(objectCreateOptions, out var staticDelegateMethodsStr, out var staticDelegateVars);

            if (!string.IsNullOrEmpty(staticDelegateVars))
            {
                sourceWriter.WriteLine(staticDelegateVars);
            }

            WriteSetRowDelimiterMethod(sourceWriter, csvMapClassInfo);
            WriteRowToHeaderMethod(sourceWriter, csvMapClassInfo);
            WriteRowToObjectMethod(sourceWriter, csvMapClassInfo, objectCreateStr);
            WriteLambdasToMethods(sourceWriter, csvMapClassInfo);

            if (!string.IsNullOrEmpty(staticDelegateMethodsStr))
            {
                sourceWriter.WriteLine(staticDelegateMethodsStr);
            }

            sourceWriter.CloseBlock(); //Class

            if (!string.IsNullOrEmpty(namespaceStr))
            {
                sourceWriter.CloseBlock(); //Namespace
            }

            CreateObjectOptions GetCreateObjectOptions()
            {
                var argValues = csvMapClassInfo.CsvMapBuilderOptions.MatchingConstructorProperties.Select(x => x.ToVarName());
                var createObjectOptions = new CreateObjectOptions(csvMapClassInfo.ClassSymbol.ContainingAssembly, csvMapClassInfo.CsvMapBuilderOptions.ChosenConstructor, argValues, allowIllegalObjCreation: true);

                foreach (var indexMap in csvMapClassInfo.CsvMapBuilderOptions.ColumnIndexMap)
                {

                    if (csvMapClassInfo.CsvMapBuilderOptions.MatchingConstructorProperties.Contains(indexMap.Key, SymbolEqualityComparer.Default))
                        continue;

                    createObjectOptions.AddPropertyValue(indexMap.Key, indexMap.Key.ToVarName(), true);
                }

                foreach (var nameMap in csvMapClassInfo.CsvMapBuilderOptions.ColumnNameMap)
                {
                    if (csvMapClassInfo.CsvMapBuilderOptions.MatchingConstructorProperties.Contains(nameMap.Key, SymbolEqualityComparer.Default))
                        continue;

                    createObjectOptions.AddPropertyValue(nameMap.Key, nameMap.Key.ToVarName(), true);
                }

                return createObjectOptions;
            }
        }

        private static void WriteLambdasToMethods(SourceWriter sourceWriter, CsvMapClassInfo csvMapClassInfo)
        {

            foreach (var map in csvMapClassInfo.CsvMapBuilderOptions.ColumnIndexMap.Where(x => x.Value.CustomParseMethod?.Name == ""))
            {
                WriteLambda(map);
            }

            foreach (var map in csvMapClassInfo.CsvMapBuilderOptions.ColumnNameMap.Where(x => x.Value.CustomParseMethod?.Name == ""))
            {
                WriteLambda(map);
            }

            void WriteLambda(KeyValuePair<IPropertySymbol, MappingInfo> map)
            {
                var lambdaMethodSymbol = map.Value.CustomParseMethod;
                bool isStatic = lambdaMethodSymbol.IsStatic;

                var lambdaSyntax = lambdaMethodSymbol.DeclaringSyntaxReferences[0].GetSyntax() as ParenthesizedLambdaExpressionSyntax;

                var isBlock = lambdaSyntax.DescendantNodes().OfType<BlockSyntax>().Any();

                sourceWriter.WriteLine($"private {(isStatic ? "static " : "")}{map.Key.Type} {GetLambdaName(map.Key.Name)}({string.Join(", ", lambdaMethodSymbol.Parameters.Select(x => $"{x.Type} {x.Name}"))}){(!isBlock ? $" => {lambdaSyntax.Body};" : "")}");

                if (isBlock)
                {
                    sourceWriter.WriteLine(lambdaSyntax.Body.ToString());
                }

                sourceWriter.WriteLine("");
            }
        }
        private static string GetLambdaName(string baseName) => $"__{baseName}_CustomParse";
        private static void WriteSetRowDelimiterMethod(SourceWriter sourceWriter, CsvMapClassInfo csvMapClassInfo)
        {
            var rowDelimiter = csvMapClassInfo.CsvMapBuilderOptions.RowDelimiter;

            sourceWriter.OpenBlock($"protected override void __SetRowDelimiter(Encoding encoding)"); //Method

            sourceWriter.WriteLine($@"
if (encoding is UTF8Encoding || encoding is ASCIIEncoding)
{{
    _rowDelimiter = new byte[] {{ {string.Join(", ", Encoding.UTF8.GetBytes(rowDelimiter).Select(x => x.ToString()))} }};
}}
else if (encoding is UnicodeEncoding)
{{
    _rowDelimiter = new byte[] {{ {string.Join(", ", Encoding.Unicode.GetBytes(rowDelimiter).Select(x => x.ToString()))} }};
}}
else if (encoding is UTF32Encoding)
{{
    _rowDelimiter = new byte[] {{ {string.Join(", ", Encoding.UTF32.GetBytes(rowDelimiter).Select(x => x.ToString()))} }};
}}
else if (encoding.Equals(Encoding.BigEndianUnicode))
{{
    _rowDelimiter = new byte[] {{ {string.Join(", ", Encoding.BigEndianUnicode.GetBytes(rowDelimiter).Select(x => x.ToString()))} }};
}}
else
{{
    throw new NotSupportedException($""Encoding '{{encoding.EncodingName}}' is not supported."");
}}
");

            sourceWriter.CloseBlock(); //Method
        }
        private static void WriteRowToHeaderMethod(SourceWriter sourceWriter, CsvMapClassInfo csvMapClassInfo)
        {
            var trimChars = csvMapClassInfo.CsvMapBuilderOptions.HeaderTrimChars;
            var charsToRemove = csvMapClassInfo.CsvMapBuilderOptions.HeaderRemoveChars;

            sourceWriter.OpenBlock($"protected override void __RowToHeader(ReadOnlySpan<char> rowSpan, long rowIndex)"); //Method
            sourceWriter.WriteLine($@"
//if (HeaderRow != null)
//{{
//    throw new InvalidOperationException(""HeaderRow already set."");
//}}

HeaderRow = new List<string>();
var rowReader = new SpanReaderChar(rowSpan);
");

            WriteDelimiterWhile(sourceWriter, csvMapClassInfo.CsvMapBuilderOptions.HeaderDelimiter); //While

            if (trimChars.Any())
            {
                sourceWriter.WriteLine($@"
colSpan = colSpan.Trim([{string.Join(", ", trimChars.Select(x => $"'{WriteDelimiterChar(x)}'"))}]);");
            }

            if (charsToRemove == null || charsToRemove.Length == 0)
            {
                sourceWriter.WriteLine($@"
var headerCol = colSpan.ToString();");
            }
            else
            {
                sourceWriter.WriteLine($@"
var headerCol = colSpan.RemoveAndToString([{string.Join(", ", charsToRemove.Select(x => $"'{WriteDelimiterChar(x)}'"))}]);");

            }

            sourceWriter.WriteLine($@"
if (!string.IsNullOrWhiteSpace(headerCol))
{{
    HeaderRow.Add(headerCol);
}}
");

            sourceWriter.CloseBlock(); //While



            sourceWriter.CloseBlock(); //Method
        }
        private static void WriteRowToObjectMethod(SourceWriter sourceWriter, CsvMapClassInfo csvMapClassInfo, string objectCreateStr)
        {
            var csvOptions = csvMapClassInfo.CsvMapBuilderOptions;

            sourceWriter.OpenBlock($"protected override {csvMapClassInfo.CsvType} __RowToObject(ReadOnlySpan<char> rowSpan, long rowIndex)"); //Method

            sourceWriter.WriteLine($@"
var colIndex = 0;
{(!csvOptions.AllowTooFewColumns ? "var matchingColCount = 0;" : "")}

 var rowReader = new SpanReaderChar(rowSpan);
");

            if (csvOptions.ColumnNameMap.Any())
                WriteHeaderRowCheck();

            WriteMapVariables();

            WriteDelimiterWhile(sourceWriter, csvOptions.ColumnDelimiter, csvOptions.StringDelimiter, csvOptions.StringDelimiterEscape); //While

            WriteTrim();

            WriteMap();

            sourceWriter.WriteLine($@"
colIndex++;
");

            sourceWriter.CloseBlock(); //While

            var expectedMatchingColCount = csvOptions.ColumnIndexMap.Count + csvOptions.ColumnNameMap.Count;

            if (!csvOptions.AllowTooFewColumns)
            {
                sourceWriter.WriteLine($@"
if (matchingColCount < {expectedMatchingColCount})
{{
    throw new InvalidOperationException($""Row number: {{rowIndex}} - Not all columns were matched to properties. Expected {expectedMatchingColCount}, only matched: {{matchingColCount}}"");
}}");
            }

            sourceWriter.WriteLine($@"
{objectCreateStr}

return newObject;
");

            sourceWriter.CloseBlock(); //Method

            void WriteHeaderRowCheck()
            {
                if (csvOptions.ColumnNameMap.Any())
                {
                    sourceWriter.OpenBlock($"if (HeaderRow == null)");
                    sourceWriter.WriteLine($"throw new InvalidOperationException(\"You cannot read CSV data before reading the header row, when using a CSV column that is mapped by name.\");");
                    sourceWriter.CloseBlock();
                }
            }

            void WriteMapVariables()
            {
                foreach (var colMap in csvOptions.ColumnIndexMap)
                {
                    sourceWriter.WriteLine($"{colMap.Key.Type} {colMap.Key.ToVarName()} = default;");
                }

                foreach (var colMap in csvOptions.ColumnNameMap)
                {
                    sourceWriter.WriteLine($"{colMap.Key.Type} {colMap.Key.ToVarName()} = default;");
                }
            }

            void WriteTrim()
            {
                var trimStringDelimiter = csvOptions.StringDelimiter != default && csvOptions.TrimStringDelimiter;

                if (trimStringDelimiter)
                {
                    if (csvOptions.StringTrimOption == StringTrim.TrimUntilStringDelimiter)
                    {
                        if (csvOptions.ColumnTrimChars.Any())
                            sourceWriter.WriteLine($"colSpan = colSpan.Trim([{string.Join(", ", csvOptions.ColumnTrimChars.Select(x => $"'{WriteDelimiterChar(x)}'"))}]);\r\n");

                        sourceWriter.WriteLine($"colSpan = colSpan.Trim('{WriteDelimiterChar(csvOptions.StringDelimiter)}');\r\n");
                    }
                    else
                    {
                        if (csvOptions.ColumnTrimChars.Any())
                            sourceWriter.WriteLine($"colSpan = colSpan.Trim([{string.Join(", ", csvOptions.ColumnTrimChars.Select(x => $"'{WriteDelimiterChar(x)}'"))}, '{WriteDelimiterChar(csvOptions.StringDelimiter)}']);\r\n");
                        else
                            sourceWriter.WriteLine($"colSpan = colSpan.Trim('{WriteDelimiterChar(csvOptions.StringDelimiter)}');\r\n");
                    }
                }
                else if (csvOptions.ColumnTrimChars.Any())
                {
                    sourceWriter.WriteLine($"colSpan = colSpan.Trim([{string.Join(", ", csvOptions.ColumnTrimChars.Select(x => $"'{WriteDelimiterChar(x)}'"))}]);\r\n");
                }

            }

            void WriteMap()
            {
                for (int i = 0; i < csvOptions.ColumnIndexMap.Count; i++)
                {
                    var colMap = csvOptions.ColumnIndexMap.ElementAt(i);

                    //Check to see if the "if" should be an "else if"
                    sourceWriter.OpenBlock($"{(i > 0 ? "else " : "")}if (colIndex == {colMap.Value.CsvColumnIndex})");

                    WriteColumnMap(colMap.Key, colMap.Value);

                    sourceWriter.CloseBlock();
                }

                for (int i = 0; i < csvOptions.ColumnNameMap.Count; i++)
                {
                    var colMap = csvOptions.ColumnNameMap.ElementAt(i);

                    //Check to see if the "if" should be an "else if"
                    var secondOrMore = i > 0 || csvOptions.ColumnIndexMap.Count > 0;

                    sourceWriter.OpenBlock($@"{(secondOrMore ? "else " : "")}if (colIndex < HeaderRow.Count && HeaderRow[colIndex].Equals(""{colMap.Value.CsvColumnName}"", StringComparison.{csvOptions.HeaderStringComparison}))");

                    WriteColumnMap(colMap.Key, colMap.Value);

                    sourceWriter.CloseBlock();
                }

                void WriteColumnMap(IPropertySymbol propSymbol, MappingInfo mappingInfo)
                {
                    bool isString = false;
                    var underlyingType = propSymbol.Type.NullableAnnotation == NullableAnnotation.Annotated ? (propSymbol.Type as INamedTypeSymbol).TypeArguments[0] : propSymbol.Type;
                    var isNullable = propSymbol.Type.NullableAnnotation == NullableAnnotation.Annotated;

                    if (mappingInfo.CustomParseMethod != null)
                    {
                        if (mappingInfo.CustomParseMethod.Name == "")
                            sourceWriter.WriteLine($@"{propSymbol.ToVarName()} = {GetLambdaName(propSymbol.Name)}(colSpan, rowIndex, colIndex);");
                        else
                            sourceWriter.WriteLine($@"{propSymbol.ToVarName()} = {mappingInfo.CustomParseMethod.Name}(colSpan, rowIndex, colIndex);");
                    }
                    else if (underlyingType.SpecialType == SpecialType.System_String)
                    {
                        isString = true;
                        sourceWriter.WriteLine($@"{propSymbol.ToVarName()} = colSpan.ToString();");
                    }
                    else
                    {
                        if (isNullable)
                        {
                            sourceWriter.WriteLine($@"{underlyingType} {propSymbol.ToVarName()}_NotNull = default;");
                            sourceWriter.OpenBlock($@"if (colSpan.IsNullOrWhiteSpace())");
                            sourceWriter.WriteLine($@"//It's already null");
                            sourceWriter.CloseBlock();
                        }

                        if (underlyingType.SpecialType == SpecialType.System_Enum)
                        {
                            sourceWriter.OpenBlock($@"{(isNullable ? $"else " : "")}if (!Enum.TryParse<{propSymbol.Type}>(colSpan, out {propSymbol.ToVarName()}))");
                        }
                        else
                        {
                            sourceWriter.OpenBlock($@"{(isNullable ? $"else " : "")}if (!{underlyingType}.TryParse(colSpan, out {propSymbol.ToVarName()}{(isNullable ? "_NotNull" : "")}))");
                        }

                        if (mappingInfo.ParseFailure == ParseFailure.ThrowException)
                        {
                            sourceWriter.WriteLine($@"throw new CsvParseException($""Parsing exception. Value '{{colSpan}}' could not be parsed to type '{propSymbol.Type}'"", rowIndex, colIndex, ""{propSymbol.Name}"", ""{mappingInfo.CsvColumnName}"");");
                        }
                        else if (mappingInfo.ParseFailure == ParseFailure.SkipColumn)
                        {
                            sourceWriter.WriteLine($@"//Skip");
                        }
                        else if (mappingInfo.ParseFailure == ParseFailure.SetValueTo)
                        {
                            sourceWriter.WriteLine($@"{propSymbol.Name} = {(isString ? $@"""{mappingInfo.SetToValue}""" : mappingInfo.SetToValue)};");
                        }

                        sourceWriter.CloseBlock();
                    }

                    if (isNullable && !isString && underlyingType.SpecialType != SpecialType.System_Enum)
                    {
                        sourceWriter.WriteLine($"\r\n{propSymbol.ToVarName()} = {propSymbol.ToVarName()}_NotNull;\r\n");
                    }

                    if (!csvOptions.AllowTooFewColumns)
                        sourceWriter.WriteLine($"\r\nmatchingColCount++;");

                }
            }

        }

        private static void WriteDelimiterWhile(SourceWriter sourceWriter, string delimiter, char stringDelimiter = default, char stringDelimiterEscape = default)
        {
            sourceWriter.Write(@"
while (");


            var writeStringDelimiter = stringDelimiter == default ? "" : $", '{WriteDelimiterChar(stringDelimiter)}'{(stringDelimiterEscape == default ? "" : $", '{WriteDelimiterChar(stringDelimiterEscape)}'")}";

            if (delimiter.Length == 1)
            {
                sourceWriter.Sb.AppendLine($@"rowReader.TryReadTo('{WriteDelimiterChar(delimiter[0])}', out var colSpan{writeStringDelimiter}) ||");
            }
            else
            {
                var splitDelimiter = "";

                for (int c = 0; c < delimiter.Length; c++)
                {
                    if (c == delimiter.Length - 1)
                        splitDelimiter += $"'{WriteDelimiterChar(delimiter[c])}'";
                    else
                        splitDelimiter += $"'{WriteDelimiterChar(delimiter[c])}', ";
                }


                sourceWriter.Sb.AppendLine($@"rowReader.TryReadTo([{splitDelimiter}], out var colSpan{writeStringDelimiter}) ||");
            }

            sourceWriter.WriteLine("       rowReader.TryGetUnread(out colSpan, true))");
            sourceWriter.OpenBlock();
        }
        private static string WriteDelimiterChar(char c)
        {
            if (c == '\r')
                return @"\r";
            else if (c == '\n')
                return @"\n";
            else if (c == '\t')
                return @"\t";
            else if (c == '\"')
                return @"""";
            else if (c == '\\')
                return @"\\";

            else
                return c.ToString();
        }
    }
}
