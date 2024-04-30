using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ByteSocket.RocketCsv.Core
{
    public class CsvParseException : Exception
    {
        public CsvParseException(string message, long rowIndex, int columnIndex, string propertyName, string csvColumnName = "") : base(message)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            PropertyName = propertyName;
            CsvColumnName = csvColumnName;
        }

        public CsvParseException(string message, Exception innerException, long rowIndex, int columnIndex, string propertyName, string csvColumnName = "") : base(message, innerException)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            PropertyName = propertyName;
            CsvColumnName = csvColumnName;
        }

        public long RowIndex { get; }
        public int ColumnIndex { get; }
        public string PropertyName { get; }
        public string CsvColumnName { get; }
    }
}
