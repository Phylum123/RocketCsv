using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.SourceGenerator.Shared
{
    public delegate R ReadOnlySpanFunc<T, R>(ReadOnlySpan<T> span);
    public delegate R ReadOnlySpanFunc<T, P1, R>(ReadOnlySpan<T> span, P1 param1);
    public delegate R ReadOnlySpanFunc<T, P1, P2, R>(ReadOnlySpan<T> span, P1 param1, P2 param2);
    public delegate R ReadOnlySpanFunc<T, P1, P2, P3, R>(ReadOnlySpan<T> span, P1 param1, P2 param2, P3 param3);
    public delegate R ReadOnlySpanFunc<T, P1, P2, P3, P4, R>(ReadOnlySpan<T> span, P1 param1, P2 param2, P3 param3, P4 param4);
    public delegate R ReadOnlySpanFunc<T, P1, P2, P3, P4, P5, R>(ReadOnlySpan<T> span, P1 param1, P2 param2, P3 param3, P4 param4, P5 param5);
    public delegate R ReadOnlySpanFunc<T, P1, P2, P3, P4, P5, P6, R>(ReadOnlySpan<T> span, P1 param1, P2 param2, P3 param3, P4 param4, P5 param5, P6 param6);
}

