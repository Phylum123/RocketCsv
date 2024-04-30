//using DotNext;
//using DotNext.Buffers;
//using DotNext.IO.MemoryMappedFiles;
//using System;
//using System.Buffers;
//using System.Diagnostics.CodeAnalysis;
//using System.IO;
//using System.IO.MemoryMappedFiles;
//using System.IO.Pipelines;
//using System.Linq.Expressions;
//using System.Reflection.PortableExecutable;
//using System.Runtime;
//using System.Runtime.CompilerServices;
//using System.Text;

//namespace ByteSocket.RocketCsv.Core
//{


//    //    //TODO: Different idea? Generate ALL of this
//    //    //-------------------------------------------------------------------------------------------------------------------
//    public class CsvMapObject : CsvMapBase<object>
//    {
//        protected internal override void __RowToHeader(ReadOnlySpan<char> rowSpan, long rowNum)
//        {
//            throw new NotImplementedException();
//        }

//        protected internal override object __RowToObject(ReadOnlySpan<char> rowSpan, long rowNum)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public abstract class CsvMapCollection
//    {
//        public static CsvMapBase<T> GetCsvMap<T>() => __CsvMapHelper<T>.CsvMap;

//        private static class __CsvMapHelper<T>
//        {
//            //This should run just once.
//            public static readonly CsvMapBase<T> CsvMap = typeof(T) == typeof(object) ? new CsvMapObject() as CsvMapBase<T> : throw new Exception("Unregistered type. Please register a csv map for type '' in 'nameOfClass'");
//        }
//    }

//    //-------------------------------------------------------------------------------------------------------------------

//    public abstract class CsvReaderBase : IDisposable
//    {
//        private static class __CsvMapHelper<T>
//        {
//            //This should run just once.
//            public static readonly CsvMapBase<T> CsvMap = typeof(T) == typeof(object) ? new CsvMapObject() as CsvMapBase<T> : throw new Exception("Unregistered type. Please register a csv map for type '' in 'nameOfClass'");
//        }

//        private readonly PoolingBufferWriter<char> _charBufferWriter = new PoolingBufferWriter<char>(MemoryPool<char>.Shared.ToAllocator());
//        private readonly PipeReader _pipeReader;
//        private Encoding? _encoding;
//        private bool disposedValue;
//        private MemoryMappedFile? _memoryMappedFile = null;

//        public CsvReaderBase(string filePath, StreamPipeReaderOptions? streamPipeReaderOptions = null, Encoding? encoding = null)
//        {
//            _memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0);

//            _encoding = encoding;

//            //Create a stream pipe reader for reading the csv file
//            _pipeReader = PipeReader.Create(_memoryMappedFile.CreateViewStream(), streamPipeReaderOptions);
//        }

//        public CsvReaderBase(Stream stream, StreamPipeReaderOptions? streamPipeReaderOptions = null, Encoding? encoding = null)
//        {

//            if (!stream.CanRead)
//            {
//                throw new ArgumentException("Stream must be readable", nameof(stream));
//            }

//            _encoding = encoding;

//            //Create a stream pipe reader for reading the csv file
//            _pipeReader = PipeReader.Create(stream, streamPipeReaderOptions);
//        }

//        protected abstract CsvMapBase<T> GetCsvMap<T>();
//        //protected override CsvMapBase<T> __GetCsvMap<T>() => __CsvMapHelper<T>.CsvMap ?? throw new Exception("Unregistered type. Please register a csv map for type '' in 'nameOfClass' using the '' attribute"); 

//        public long CurrentRowNum { get; private set; } = -1;

//        /// <summary>
//        /// Determines a text file's encoding by analyzing its byte order mark (BOM).
//        /// Defaults to Encoding.Default when detection of the text file's endianness fails.
//        /// </summary>
//        /// <param name="filename">The text file to analyze.</param>
//        /// <returns>The detected encoding.</returns>
//        private static Encoding GetEncoding(ref ReadOnlySequence<byte> buffer)
//        {
//            if (buffer.Length < 4)
//            {
//                throw new Exception("Buffer is too small to determine encoding"); //TODO: Lets not error this, lets try again, becasue there might be a weird case where you could read less than 4 bytes.
//            }

//            var bom = buffer.FirstSpan;

//            // Analyze the BOM
//            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
//            {
//                buffer = buffer.Slice(3);

//                return Encoding.UTF7;
//            }
//            else if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
//            {
//                buffer = buffer.Slice(3);

//                return Encoding.UTF8;
//            }
//            else if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0)
//            {
//                buffer = buffer.Slice(4);

//                return Encoding.UTF32; // UTF-32LE
//            }
//            else if (bom[0] == 0xff && bom[1] == 0xfe)
//            {
//                buffer = buffer.Slice(2);

//                return Encoding.Unicode; // UTF-16LE
//            }
//            else if (bom[0] == 0xfe && bom[1] == 0xff)
//            {
//                buffer = buffer.Slice(2);

//                return Encoding.BigEndianUnicode; // UTF-16BE
//            }
//            else if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
//            {
//                buffer = buffer.Slice(4);

//                return new UTF32Encoding(true, true); // UTF-32BE
//            }

//            return Encoding.Default;
//        }

//        public async ValueTask ReadHeaderRowAsync<T>(CancellationToken cancelToken = default)
//        {
//            var csvMap = GetCsvMap<T>();
//            var readResult = await ReadNextBuffer(cancelToken).ConfigureAwait(false);

//            if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//            {
//                throw new Exception("Failed to read header row.");
//            }

//            GetHeaderRow();

//            void GetHeaderRow()
//            {
//                var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                if (!TryReadBufferRow(ref seqReader, readResult.IsCompleted, out var headerRow))
//                {
//                    throw new Exception("Failed to read header row.");
//                }

//                _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);

//                csvMap.__RowToHeader(headerRow.Span, CurrentRowNum);
//            }

//        }

//        public async ValueTask FindHeaderRowAsync<T>(SearchType searchType, string strToFind, StringComparison stringComparison = StringComparison.Ordinal, CancellationToken cancelToken = default)
//        {
//            var csvMap = GetCsvMap<T>();

//            while (true)
//            {
//                var readResult = await ReadNextBuffer().ConfigureAwait(false);

//                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//                {
//                    throw new Exception("Failed to find header row.");
//                }

//                GetHeaderRows();

//                if (csvMap.HeaderRow != null)
//                {
//                    return;
//                }

//                void GetHeaderRows()
//                {
//                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                    while (TryReadBufferRow(ref seqReader, readResult.IsCompleted, out var rowMemory))
//                    {
//                        var rowSpan = rowMemory.Span;

//                        if (searchType == SearchType.StartsWith &&
//                            rowSpan.StartsWith(strToFind, stringComparison))
//                        {
//                            csvMap.__RowToHeader(rowSpan, CurrentRowNum);
//                            break;
//                        }
//                        else if (searchType == SearchType.Contains &&
//                                 rowSpan.Contains(strToFind, stringComparison))
//                        {
//                            csvMap.__RowToHeader(rowSpan, CurrentRowNum);
//                            break;
//                        }
//                        else if (searchType == SearchType.Equals &&
//                                 rowSpan.Equals(strToFind, stringComparison))
//                        {
//                            csvMap.__RowToHeader(rowSpan, CurrentRowNum);
//                            break;
//                        }
//                        else if (searchType == SearchType.EndsWith &&
//                                 rowSpan.EndsWith(strToFind, stringComparison))
//                        {
//                            csvMap.__RowToHeader(rowSpan, CurrentRowNum);
//                            break;
//                        }
//                    }

//                    _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);
//                }
//            }

//        }

//        public async ValueTask FindHeaderCustomAsync<T>(Func<ReadOnlyMemory<char>, List<string>?> customHeaderFunc, CancellationToken cancelToken = default)
//        {
//            var csvMap = GetCsvMap<T>();

//            while (true)
//            {
//                var readResult = await ReadNextBuffer().ConfigureAwait(false);

//                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//                {
//                    throw new Exception("Failed to find header row.");
//                }

//                GetHeaderRows();

//                if (csvMap.HeaderRow != null)
//                {
//                    return;
//                }

//                void GetHeaderRows()
//                {
//                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                    while (TryReadBufferRow(ref seqReader, readResult.IsCompleted, out var rowMemory))
//                    {
//                        var headerRow = customHeaderFunc.Invoke(rowMemory);

//                        if (headerRow != null)
//                        {
//                            csvMap.HeaderRow = headerRow;
//                            break;
//                        }
//                    }

//                    _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);
//                }
//            }

//        }

//        public async ValueTask<IEnumerable<T>> ReadDataRowsAsync<T>(int numRows = int.MaxValue, CancellationToken cancelToken = default)
//        {
//            var csvMap = GetCsvMap<T>();

//            var objList = new List<T>(numRows <= 1000 ? numRows : default);
//            int rowCount = 0;

//            while (rowCount < numRows)
//            {
//                var readResult = await ReadNextBuffer().ConfigureAwait(false);

//                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//                {
//                    break;
//                }

//                GetDataRows();

//                void GetDataRows()
//                {
//                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                    while (rowCount < numRows && TryReadBufferRow(ref seqReader, readResult.IsCompleted, out var rowMemory))
//                    {
//                        objList.Add(csvMap.__RowToObject(rowMemory.Span, CurrentRowNum));

//                        rowCount++;
//                    }

//                    _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);
//                }


//            }

//            return objList;
//        }

//        public async ValueTask<IEnumerable<T>> ReadDataRowsUntilAsync<T>(SearchType searchType, string strToFind, StringComparison stringComparison = StringComparison.Ordinal, CancellationToken cancelToken = default)
//        {
//            var csvMap = GetCsvMap<T>();
//            var objList = new List<T>();

//            while (true)
//            {
//                var readResult = await ReadNextBuffer().ConfigureAwait(false);

//                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//                {
//                    break;
//                }

//                GetDataRows();

//                void GetDataRows()
//                {
//                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                    while (TryReadBufferRow(ref seqReader, readResult.IsCompleted, out var rowMemory))
//                    {
//                        var rowSpan = rowMemory.Span;

//                        if (searchType == SearchType.StartsWith &&
//                            rowSpan.StartsWith(strToFind, stringComparison))
//                        {
//                            break;
//                        }
//                        else if (searchType == SearchType.Contains &&
//                                 rowSpan.Contains(strToFind, stringComparison))
//                        {
//                            break;
//                        }
//                        else if (searchType == SearchType.Equals &&
//                                 rowSpan.Equals(strToFind, stringComparison))
//                        {
//                            break;
//                        }
//                        else if (searchType == SearchType.EndsWith &&
//                                 rowSpan.EndsWith(strToFind, stringComparison))
//                        {
//                            break;
//                        }

//                        objList.Add(csvMap.__RowToObject(rowSpan, CurrentRowNum));
//                    }

//                    _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);
//                }
//            }

//            return objList;
//        }

//        public async ValueTask<int> SkipRowsAsync(int numRows, CancellationToken cancelToken = default)
//        {
//            int rowCount = 0;

//            while (rowCount < numRows)
//            {
//                var readResult = await ReadNextBuffer().ConfigureAwait(false);

//                if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//                {
//                    break;
//                }

//                SkipRows();

//                void SkipRows()
//                {
//                    var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                    while (rowCount < numRows && TryReadBufferRow(ref seqReader, readResult.IsCompleted, out _, true))
//                    {
//                        rowCount++;
//                    }

//                    _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);
//                }
//            }

//            return rowCount;
//        }

//        public async ValueTask<string> ReadRowAsStringAsync(CancellationToken cancelToken = default)
//        {
//            var readResult = await ReadNextBuffer().ConfigureAwait(false);

//            if (readResult.IsCanceled || (readResult.IsCompleted && readResult.Buffer.IsEmpty))
//            {
//                throw new Exception("Failed to read row as string.");
//            }

//            return GetStringRow();

//            string GetStringRow()
//            {
//                var seqReader = new SequenceReader<byte>(readResult.Buffer);

//                if (!TryReadBufferRow(ref seqReader, readResult.IsCompleted, out var rowMemory))
//                {
//                    throw new Exception("Failed to read row as string.");
//                }

//                _pipeReader.AdvanceTo(seqReader.Position, readResult.Buffer.End);

//                return rowMemory.ToString();
//            }

//        }



//        private async ValueTask<ReadResult> ReadNextBuffer(CancellationToken cancelToken = default)
//        {
//            var readResult = await _pipeReader.ReadAsync(cancelToken).ConfigureAwait(false);

//            if (_encoding == null && !readResult.IsCanceled && !readResult.Buffer.IsEmpty)
//            {
//                var buffer = readResult.Buffer;

//                _encoding = GetEncoding(ref buffer);

//                return new ReadResult(buffer, readResult.IsCanceled, readResult.IsCompleted);
//            }

//            return readResult;
//        }

//        private bool TryReadBufferRow(ref SequenceReader<byte> seqReader, bool isCompleted, out ReadOnlyMemory<char> rowMemory, bool skipRow = false)
//        {
//            rowMemory = default;

//            if (seqReader.TryReadTo(out ReadOnlySpan<byte> rowSpan, 0xA)) // \n - newline
//            {
//                if (rowSpan[rowSpan.Length - 1] == 0xD) // \r - carriage return
//                {
//                    rowSpan = rowSpan.Slice(0, rowSpan.Length - 1);
//                }

//            }
//            else if (isCompleted)
//            {
//                var unreadSpan = seqReader.UnreadSpan.Trim((byte)'\0'); //Get rid of nulls at the end of the buffer

//                if (!unreadSpan.IsEmpty)
//                {
//                    rowSpan = unreadSpan;
//                }

//                seqReader.AdvanceToEnd();
//            }

//            //Nothing left to read in the buffer
//            if (rowSpan.IsEmpty)
//            {
//                return false;
//            }

//            //Increase the row number. It starts at -1, so the first row is 0 - to be consistant with 0 based column numbers
//            CurrentRowNum++;

//            //Skip row byte conversion to chars
//            if (skipRow)
//            {
//                return true;
//            }

//            //Convert the row bytes to chars, using the encoding
//            _charBufferWriter.Clear(true);

//            _encoding.GetChars(rowSpan, _charBufferWriter);

//            rowMemory = _charBufferWriter.WrittenMemory;

//            return true;
//        }

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!disposedValue)
//            {
//                if (disposing)
//                {
//                    // TODO: dispose managed state (managed objects)
//                    _charBufferWriter.Dispose();
//                    _pipeReader.Complete();
//                    _memoryMappedFile?.Dispose();
//                }

//                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
//                // TODO: set large fields to null
//                disposedValue = true;
//            }
//        }

//        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
//        // ~CsvReaderBase()
//        // {
//        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
//        //     Dispose(disposing: false);
//        // }

//        public void Dispose()
//        {
//            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
//            Dispose(disposing: true);
//            GC.SuppressFinalize(this);
//        }
//    }
//}
