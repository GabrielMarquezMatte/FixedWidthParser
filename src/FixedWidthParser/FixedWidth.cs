using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using System.Text;

namespace FixedWidthParser
{
    /// <summary>
    /// Entry points for the source-generated, reflection-free parse path. Dispatches to the model's
    /// generated <see cref="IFixedWidthModel{TSelf}.TryParse"/> through the type parameter, so the call
    /// is statically resolved (no reflection, no delegates, JIT-devirtualized).
    /// </summary>
    public static class FixedWidth
    {
        /// <summary>Parses a single fixed-width line into <typeparamref name="TModel"/> using its generated parser.</summary>
        public static bool TryParse<TModel>(ReadOnlySpan<char> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
            where TModel : IFixedWidthModel<TModel>, allows ref struct
        {
            return TModel.TryParse(line, formatProvider, stringPool, out model);
        }

        /// <summary>Reads source-generated models from an existing <see cref="TextReader"/>.</summary>
        public static GeneratedFixedWidthRecordEnumerable<TModel> Read<TModel>(
            TextReader reader,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(reader);
            ValidateBufferSize(bufferSize);
            return new GeneratedFixedWidthRecordEnumerable<TModel>(
                TextReaderSource.FromReader(reader), formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a <see cref="Stream"/>.</summary>
        public static GeneratedFixedWidthRecordEnumerable<TModel> Read<TModel>(
            Stream stream,
            Encoding? encoding = null,
            bool leaveOpen = false,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateBufferSize(bufferSize);
            var enc = encoding ?? Encoding.UTF8;
            return new GeneratedFixedWidthRecordEnumerable<TModel>(
                TextReaderSource.FromStream(stream, enc, leaveOpen), formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a file, reopening it for each enumeration.</summary>
        public static GeneratedFixedWidthRecordEnumerable<TModel> ReadFile<TModel>(
            string path,
            Encoding? encoding = null,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ValidateBufferSize(bufferSize);
            var enc = encoding ?? Encoding.UTF8;
            return new GeneratedFixedWidthRecordEnumerable<TModel>(
                TextReaderSource.FromFile(path, enc, useAsync: false), formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from an existing <see cref="TextReader"/> via <c>await foreach</c>.</summary>
        public static GeneratedFixedWidthAsyncRecordEnumerable<TModel> ReadAsync<TModel>(
            TextReader reader,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(reader);
            ValidateBufferSize(bufferSize);
            return new GeneratedFixedWidthAsyncRecordEnumerable<TModel>(
                TextReaderSource.FromReader(reader), formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a <see cref="Stream"/> via <c>await foreach</c>.</summary>
        public static GeneratedFixedWidthAsyncRecordEnumerable<TModel> ReadAsync<TModel>(
            Stream stream,
            Encoding? encoding = null,
            bool leaveOpen = false,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateBufferSize(bufferSize);
            var enc = encoding ?? Encoding.UTF8;
            return new GeneratedFixedWidthAsyncRecordEnumerable<TModel>(
                TextReaderSource.FromStream(stream, enc, leaveOpen), formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a file asynchronously, reopening it for each enumeration.</summary>
        public static GeneratedFixedWidthAsyncRecordEnumerable<TModel> ReadFileAsync<TModel>(
            string path,
            Encoding? encoding = null,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IFixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ValidateBufferSize(bufferSize);
            var enc = encoding ?? Encoding.UTF8;
            return new GeneratedFixedWidthAsyncRecordEnumerable<TModel>(
                TextReaderSource.FromFile(path, enc, useAsync: true), formatProvider, stringPool, bufferSize);
        }

        private static void ValidateBufferSize(int bufferSize)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);
        }
    }
}
