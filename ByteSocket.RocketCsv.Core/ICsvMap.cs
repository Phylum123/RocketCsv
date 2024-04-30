using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Core
{
    public interface ICsvMap<T>
    {
        public List<string>? HeaderRow { get; }
        public void __SetRowDelimiter(Encoding encoding);
        public void __RowToHeader(scoped ReadOnlySpan<char> rowSpan, long rowIndex);
        public T __RowToObject(scoped ReadOnlySpan<char> rowMemory, long rowIndex);
    }

}
