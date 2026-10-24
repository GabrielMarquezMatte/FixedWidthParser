using System.Runtime.CompilerServices;
using System.Text;

namespace FixedWidthParser.Native
{
    /// <summary>The calling thread's last failure, read back through <c>fw_last_error</c> and <c>fw_last_parse_error</c>.</summary>
    internal static class NativeApi
    {
        [ThreadStatic]
        private static string? _lastError;

        [ThreadStatic]
        private static long _parseLine;

        [ThreadStatic]
        private static int _parseColumn;

#pragma warning disable CA2255 // A native library has no Main; this is its only start-up hook.
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void RegisterCodePages()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        internal static void SetLastError(string message)
        {
            _lastError = message;
            _parseLine = 0;
            _parseColumn = -1;
        }

        /// <summary>Records a parse failure; <paramref name="column"/> is the layout index, -1 for a short line.</summary>
        internal static void SetParseError(FixedWidthParseException exception, int column)
        {
            _lastError = exception.Message;
            _parseLine = exception.LineNumber;
            _parseColumn = column;
        }

        internal static void LastParseError(out long line, out int column)
        {
            line = _parseLine;
            column = _lastError is null ? -1 : _parseColumn;
        }

        internal static string LastErrorText()
        {
            return _lastError ?? "";
        }

        internal static int LastError(Span<byte> buffer, out int length)
        {
            string? message = _lastError;
            if (string.IsNullOrEmpty(message))
            {
                length = 0;
                return NativeStatus.Ok;
            }

            length = Encoding.UTF8.GetByteCount(message);
            if (buffer.Length < length)
            {
                return NativeStatus.BufferTooSmall;
            }
            Encoding.UTF8.GetBytes(message, buffer);
            return NativeStatus.Ok;
        }
    }
}
