using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ByteSocket.SourceGenHelpers
{
    public class FluentMethodGroup
    {
        public FluentMethodGroup()
        {
        }

        public List<FluentMethodInfo> FluentMethods { get; } = new List<FluentMethodInfo>();
    }

    public class FluentMethodInfo
    {
        public FluentMethodInfo(InvocationExpressionSyntax fluentMethodSyntax, IMethodSymbol fluentMethodSymbol, IEnumerable<object> argumentValues)
        {
            FluentMethodSyntax = fluentMethodSyntax;
            FluentMethodSymbol = fluentMethodSymbol;
            ArgumentValues = argumentValues;
        }

        public InvocationExpressionSyntax FluentMethodSyntax { get; set; }
        public IMethodSymbol FluentMethodSymbol { get; set; }
        public IEnumerable<object> ArgumentValues { get; }
    }
}
