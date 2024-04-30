
using System.Buffers;

namespace ByteSocket.RocketCsv.Core
{
    public ref struct SpanReaderChar
    {
        private int _position;
        private ReadOnlySpan<char> _spanToRead;

        public SpanReaderChar(in ReadOnlySpan<char> spanToRead)
        {
            _spanToRead = spanToRead;
            _position = 0;
        }

        public bool TryReadTo(char delimiter, out ReadOnlySpan<char> span, char stringDelimiter = default, char stringDelimiterEscape = default)
        {
            span = default;
            bool isInsideString = false;

            for (int i = _position; i < _spanToRead.Length; i++)
            {
                if (_spanToRead[i] == stringDelimiter && i > 0 && _spanToRead[i - 1] != stringDelimiterEscape)
                {
                    isInsideString = !isInsideString;
                }
                else if (!isInsideString && _spanToRead[i] == delimiter)
                {
                    span = _spanToRead.Slice(_position, i - _position);
                    _position = i + 1;

                    return true;
                }
            }
            return false;
        }
        
        //52 length
        //12345678901234567890123456789012345678901234567890123456789012345678901234567890 Length
        //01234567890123456789012345678901234567890123456789012345678901234567890123456789 Index
        //"Hello, This, is, a test", "Hello", 12.4, "The, End"
        public bool TryReadTo(string delimiter, out ReadOnlySpan<char> span, char stringDelimiter = default, char stringDelimiterEscape = default)
        {
            span = default;
            bool isInsideString = false;

            for (int i = _position; i < _spanToRead.Length; i++)
            {
                if (_spanToRead[i] == stringDelimiter && i > 0 && _spanToRead[i - 1] != stringDelimiterEscape)
                {
                    isInsideString = !isInsideString;
                }
                else if (!isInsideString && _spanToRead.Length - i >= delimiter.Length)
                {
                    //Search for the delimiter
                    for (int d = 0; d < delimiter.Length; d++)
                    {                         
                        if (_spanToRead[i + d] != delimiter[d])
                        {
                            break;
                        }

                        //Full delimiter found
                        if (d == delimiter.Length - 1)
                        {
                            //Return the span from the current position to the delimiter we just found
                            span = _spanToRead.Slice(_position, i - _position);
                            _position = i + delimiter.Length;

                            return true;
                        }
                    }
                }
            }

            return false;
        }
 
        public bool TryGetUnread(out ReadOnlySpan<char> unreadSpan, bool markRead)
        {
            unreadSpan = default;

            if (_position < _spanToRead.Length)
            {
                unreadSpan = _spanToRead.Slice(_position);

                if (markRead)
                {
                    _position = _spanToRead.Length;
                }

                return true;
            }

            return false;
        }

    }

}

