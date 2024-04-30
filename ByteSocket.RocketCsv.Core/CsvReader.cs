using DotNext;
using DotNext.Buffers;
using DotNext.IO.MemoryMappedFiles;
using DotNext.Text;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Reflection.PortableExecutable;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;

namespace ByteSocket.RocketCsv.Core
{
    public enum SearchType
    {
        StartsWith,
        Contains,
        Equals,
        EndsWith
    }

    public class CsvReader : IDisposable
    {
        private readonly PooledBufferWriter<char> _charBufferWriter = new PooledBufferWriter<char>() { BufferAllocator = MemoryPool<char>.Shared.ToAllocator() };
        private readonly PipeReader _pipeReader;
        private bool _checkForEncoding = true;
        private bool disposedValue;

        public CsvReader(Stream stream, StreamPipeReaderOptions? streamPipeReaderOptions = null, Encoding? encoding = null)
        {

#pragma warning disable SYSLIB0001
            if (encoding == Encoding.UTF7)
            {
                Throw_NoUTF7Support();
            }

            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream must be readable", nameof(stream));
            }

            Encoding = encoding;

            //Create a stream pipe reader for reading the csv file
            _pipeReader = PipeReader.Create(stream, streamPipeReaderOptions);
        }
        public Encoding? Encoding { get; private set; }
        public long RowIndex { get; private set; } = -1;

        internal List<Action<Encoding>> CsvMapSetEncodingMethods { get; } = new List<Action<Encoding>>();

        internal async ValueTask<ReadResult> ReadNextBuffer(CancellationToken cancelToken = default)
        {
            var readResult = await _pipeReader.ReadAsync(cancelToken).ConfigureAwait(false);

            if (_checkForEncoding && !readResult.IsCanceled && !readResult.Buffer.IsEmpty)
            {
                var buffer = readResult.Buffer;

                if (Encoding == null)
                {
                    Encoding = GetEncoding(ref buffer, Encoding);
                }
                else //User provided encoding, so we check for the bytes anyway, becasue now they are garbage, and the method will skip them for us is they exist
                {
                    GetEncoding(ref buffer, Encoding);
                }

                _checkForEncoding = false;

                foreach (var setEncodingMethod in CsvMapSetEncodingMethods)
                {
                    setEncodingMethod(Encoding);
                }

                return new ReadResult(buffer, readResult.IsCanceled, readResult.IsCompleted);
            }

            return readResult;
        }

        internal void AdvanceReader(SequencePosition consumed, SequencePosition examined) => _pipeReader.AdvanceTo(consumed, examined);

        internal bool TryReadBufferRow(byte[] rowDelimiter, ref SequenceReader<byte> seqReader, bool isCompleted, out ReadOnlyMemory<char> rowMemory, bool skipRow = false)
        {
            rowMemory = default;

            if (seqReader.TryReadTo(out ReadOnlySpan<byte> rowSpan, rowDelimiter))
            {
            }
            else if (isCompleted)
            {
                var unreadSpan = seqReader.UnreadSpan.Trim((byte)'\0'); //Get rid of nulls at the end of the buffer

                if (!unreadSpan.IsEmpty)
                {
                    rowSpan = unreadSpan;
                }

                seqReader.AdvanceToEnd();
            }

            //Nothing left to read in the buffer
            if (rowSpan.IsEmpty)
            {
                return false;
            }

            //Increase the row number. It starts at -1, so the first row is 0 - to be consistant with 0 based column numbers
            RowIndex++;

            //Skip row byte conversion to chars
            if (skipRow)
            {
                return true;
            }

            //Convert the row bytes to chars, using the encoding
            _charBufferWriter.Clear(true);

            Encoding.GetChars(rowSpan, _charBufferWriter);

            rowMemory = _charBufferWriter.WrittenMemory;

            return true;
        }

        private static Encoding GetEncoding(ref ReadOnlySequence<byte> buffer, Encoding? currentEncoding)
        {
            if (buffer.Length < 4)
            {
                //TODO: Lets not error this, lets try again, becasue there might be a weird case where you could read less than 4 bytes.
                throw new Exception("Buffer is too small to determine encoding");
            }

            Span<byte> bom = stackalloc byte[4];

            buffer.Slice(0, 4).CopyTo(bom, out _);

            // Analyze the BOM 
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
            {
                Throw_NoUTF7Support();
            }
            else if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
            {
                buffer = buffer.Slice(3);

                return Encoding.UTF8;
            }
            else if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0)
            {
                buffer = buffer.Slice(4);

                return Encoding.UTF32; // UTF-32LE
            }
            else if (bom[0] == 0xff && bom[1] == 0xfe)
            {
                buffer = buffer.Slice(2);

                return Encoding.Unicode; // UTF-16LE
            }
            else if (bom[0] == 0xfe && bom[1] == 0xff)
            {
                buffer = buffer.Slice(2);

                return Encoding.BigEndianUnicode; // UTF-16BE
            }
            else if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
            {
                buffer = buffer.Slice(4);

                return new UTF32Encoding(true, true); // UTF-32BE
            }

            return Encoding.Default;
        }

        private static void Throw_NoUTF7Support()
        {
            throw new NotSupportedException("UTF-7 is not supported, due to security issues. Try UTF8 instead.");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _charBufferWriter.Dispose();
                    _pipeReader.Complete();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~CsvReaderBase()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
