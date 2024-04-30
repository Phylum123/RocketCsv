using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Core
{
    public static class SpanCharExtensions
    {

        public static string RemoveAndToString(this scoped ReadOnlySpan<char> spanToCopy, scoped ReadOnlySpan<char> charsToRemove)
        {
            if (spanToCopy.Length > 1000)
                throw new ArgumentOutOfRangeException("spanToCopy", "The span length is too big. Allowed length is 1000 or less.");

            Span<char> resultSpan = stackalloc char[spanToCopy.Length];

            int r = 0;
            for (int i = 0; i < spanToCopy.Length; i++, r++)
            {
                bool found = false;

                for (int j = 0; j < charsToRemove.Length; j++)
                {
                    if (spanToCopy[i] == charsToRemove[j])
                    {
                        r--;
                        found = true;

                        break;
                    }
                }

                if (!found)
                    resultSpan[r] = spanToCopy[i];
            }

            resultSpan = resultSpan.Slice(0, r);

            return resultSpan.ToString();
        }

        public static bool IsNullOrWhiteSpace(this ReadOnlySpan<char> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (!char.IsWhiteSpace(span[i]))
                    return false;
            }

            return true;
        }

    }




}
