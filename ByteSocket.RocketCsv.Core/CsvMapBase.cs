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
    public abstract class CsvMapBase<T>
    {
        private readonly CsvReader _csvReader;
        protected byte[]? _rowDelimiter;

        public CsvMapBase(CsvReader csvReader)
        {
            _csvReader = csvReader;

            if (csvReader.Encoding == null)
            {
                _csvReader.CsvMapSetEncodingMethods.Add(__SetRowDelimiter);
            }
            else
            {
                __SetRowDelimiter(csvReader.Encoding);
            }

        }

        public List<string>? HeaderRow { get; protected set; }

        protected abstract void __SetRowDelimiter(Encoding encoding);
        protected abstract void __RowToHeader(scoped ReadOnlySpan<char> rowSpan, long rowIndex);
        protected abstract T __RowToObject(scoped ReadOnlySpan<char> rowMemory, long rowIndex);

        public async ValueTask ReadHeaderRowAsync(CancellationToken cancelToken = default)
        {
            var readResult = await _csvReader.ReadNextBuffer(cancelToken).ConfigureAwait(false);

            if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
            {
                Throw_HeaderReadFail();
            }

            GetHeaderRow();

            void GetHeaderRow()
            {
                var seqReader = new SequenceReader<byte>(readResult.Buffer);

                if (!_csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var headerRow))
                {
                    Throw_HeaderReadFail();
                }

                __RowToHeader(headerRow.Span, _csvReader.RowIndex);

                _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

            }

        }

        public async ValueTask FindHeaderRowAsync(SearchType searchType, string strToFind, StringComparison stringComparison = StringComparison.Ordinal, CancellationToken cancelToken = default)
        {

            while (!cancelToken.IsCancellationRequested)
            {
                var readResult = await _csvReader.ReadNextBuffer(cancelToken).ConfigureAwait(false);

                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
                {
                    Throw_HeaderReadFail();
                }

                if (FindHeaderRow())
                    break;

                bool FindHeaderRow()
                {
                    bool found = false;
                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

                    while (_csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var rowMemory))
                    {
                        var rowSpan = rowMemory.Span;

                        if (searchType == SearchType.StartsWith &&
                            rowSpan.StartsWith(strToFind, stringComparison))
                        {
                            __RowToHeader(rowSpan, _csvReader.RowIndex);
                            found = true;

                            break;
                        }
                        else if (searchType == SearchType.Contains &&
                                 rowSpan.Contains(strToFind, stringComparison))
                        {
                            __RowToHeader(rowSpan, _csvReader.RowIndex);
                            found = true;

                            break;
                        }
                        else if (searchType == SearchType.Equals &&
                                 rowSpan.Equals(strToFind, stringComparison))
                        {
                            __RowToHeader(rowSpan, _csvReader.RowIndex);
                            found = true;

                            break;
                        }
                        else if (searchType == SearchType.EndsWith &&
                                 rowSpan.EndsWith(strToFind, stringComparison))
                        {
                            __RowToHeader(rowSpan, _csvReader.RowIndex);
                            found = true;

                            break;
                        }
                    }

                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

                    return found;
                }
            }

        }

        public async ValueTask FindHeaderCustomAsync(Func<ReadOnlyMemory<char>, List<string>?> customHeaderFunc, CancellationToken cancelToken = default)
        {
            while (true)
            {
                var readResult = await _csvReader.ReadNextBuffer().ConfigureAwait(false);

                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
                {
                    Throw_HeaderReadFail();
                }

                if (FindHeaderRow())
                    break;

                bool FindHeaderRow()
                {
                    var found = false;
                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

                    while (_csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var rowMemory))
                    {
                        var headerRow = customHeaderFunc.Invoke(rowMemory);

                        if (headerRow != null)
                        {
                            found = true;
                            HeaderRow = headerRow;
                            break;
                        }
                    }

                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

                    return found;
                }
            }

        }

        public async ValueTask<IEnumerable<T>> ReadDataRowsAsync(int numRows = int.MaxValue, CancellationToken cancelToken = default)
        {
            var objList = new List<T>(numRows <= 1000 ? numRows : default);
            int rowCount = 0;

            while (rowCount < numRows)
            {
                var readResult = await _csvReader.ReadNextBuffer().ConfigureAwait(false);

                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
                {
                    break;
                }

                GetDataRows();

                void GetDataRows()
                {
                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

                    while (rowCount < numRows && _csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var rowMemory))
                    {
                        objList.Add(__RowToObject(rowMemory.Span, _csvReader.RowIndex));

                        rowCount++;
                    }

                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);
                }

            }

            return objList;
        }

        public async ValueTask<IEnumerable<T>> ReadDataRowsUntilAsync(SearchType searchType, string strToFind, StringComparison stringComparison = StringComparison.Ordinal, CancellationToken cancelToken = default)
        {
            var objList = new List<T>();

            while (true)
            {
                var readResult = await _csvReader.ReadNextBuffer().ConfigureAwait(false);

                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
                {
                    break;
                }

                if (StringFound_GetData())
                {
                    break;
                }

                bool StringFound_GetData()
                {
                    var found = false;
                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

                    while (_csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var rowMemory))
                    {
                        var rowSpan = rowMemory.Span;

                        if (searchType == SearchType.StartsWith &&
                            rowSpan.StartsWith(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }
                        else if (searchType == SearchType.Contains &&
                                 rowSpan.Contains(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }
                        else if (searchType == SearchType.Equals &&
                                 rowSpan.Equals(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }
                        else if (searchType == SearchType.EndsWith &&
                                 rowSpan.EndsWith(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }

                        objList.Add(__RowToObject(rowSpan, _csvReader.RowIndex));
                    }

                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

                    return found;
                }
            }

            return objList;
        }

        public async ValueTask<int> SkipRowsAsync(int numRows, CancellationToken cancelToken = default)
        {
            int rowCount = 0;

            while (rowCount < numRows)
            {
                var readResult = await _csvReader.ReadNextBuffer().ConfigureAwait(false);

                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
                {
                    break;
                }

                SkipRows();

                void SkipRows()
                {
                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

                    while (rowCount < numRows && _csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out _, true))
                    {
                        rowCount++;
                    }

                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);
                }
            }

            return rowCount;
        }

        public async ValueTask SkipRowsUntilAsync(SearchType searchType, string strToFind, StringComparison stringComparison = StringComparison.Ordinal, CancellationToken cancelToken = default)
        {
            while (true)
            {
                var readResult = await _csvReader.ReadNextBuffer().ConfigureAwait(false);

                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
                {
                    break;
                }

                if (StringFound())
                {
                    return;
                }

                bool StringFound()
                {
                    var found = false;
                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

                    while (_csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var rowMemory)) //Don't skip the row, because we need to read it to check if we found the string we are looking for
                    {
                        var rowSpan = rowMemory.Span;

                        if (searchType == SearchType.StartsWith &&
                            rowSpan.StartsWith(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }
                        else if (searchType == SearchType.Contains &&
                                 rowSpan.Contains(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }
                        else if (searchType == SearchType.Equals &&
                                 rowSpan.Equals(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }
                        else if (searchType == SearchType.EndsWith &&
                                 rowSpan.EndsWith(strToFind, stringComparison))
                        {
                            found = true;
                            break;
                        }

                    }

                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

                    return found;
                }
            }

        }

        public async ValueTask<string> ReadRowAsStringAsync(CancellationToken cancelToken = default)
        {
            var readResult = await _csvReader.ReadNextBuffer(cancelToken).ConfigureAwait(false);

            if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
            {
                return "";
            }

            return GetStringRow();

            string GetStringRow()
            {
                var seqReader = new SequenceReader<byte>(readResult.Buffer);

                if (!_csvReader.TryReadBufferRow(_rowDelimiter, ref seqReader, readResult.IsCompleted, out var rowMemory))
                {
                    _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

                    return "";
                }

                _csvReader.AdvanceReader(seqReader.Position, readResult.Buffer.End);

                return rowMemory.ToString();
            }

        }

        private static void Throw_HeaderReadFail()
        {
            throw new Exception("Failed to read header row.");
        }
    }


}
