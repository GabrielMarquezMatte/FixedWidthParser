using System.IO.Pipelines;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;

namespace FixedWidthParser
{
    /// <summary>
    /// UTF-8 / byte counterpart of <see cref="FixedWidth"/>: entry points for the source-generated,
    /// reflection-free <em>byte</em> parse path. Dispatches to the model's generated
    /// <see cref="IUtf8FixedWidthModel{TSelf}.TryParse"/> through the type parameter, so the call is
    /// statically resolved (no reflection, no delegates, JIT-devirtualized). Reads bytes straight from a
    /// <see cref="Stream"/> — no <see cref="StreamReader"/>, no transcode, no string per line.
    /// <para><b>Column offsets are measured in bytes</b> (see <see cref="IUtf8FixedWidthModel{TSelf}"/>).</para>
    /// </summary>
    public static class FixedWidthUtf8
    {
        /// <summary>Parses a single UTF-8 fixed-width line into <typeparamref name="TModel"/> using its generated parser.</summary>
        public static bool TryParse<TModel>(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, StringPool? stringPool, out TModel model)
#if NET9_0_OR_GREATER
            where TModel : IUtf8FixedWidthModel<TModel>, allows ref struct
#else
            where TModel : IUtf8FixedWidthModel<TModel>
#endif
        {
            return TModel.TryParse(line, formatProvider, stringPool, out model);
        }


        /// <summary>Reads source-generated models from a <see cref="Stream"/> as raw bytes (single pass).</summary>
        public static GeneratedUtf8FixedWidthRecordEnumerable<TModel> Read<TModel>(
            Stream stream,
            bool leaveOpen = false,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IUtf8FixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateBufferSize(bufferSize);
            return new GeneratedUtf8FixedWidthRecordEnumerable<TModel>(
                stream, ownsStream: !leaveOpen, formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a file as raw bytes, reopening it for each enumeration.</summary>
        public static GeneratedUtf8FixedWidthRecordEnumerable<TModel> ReadFile<TModel>(
            string path,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IUtf8FixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ValidateBufferSize(bufferSize);
            return new GeneratedUtf8FixedWidthRecordEnumerable<TModel>(
                path, formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a <see cref="Stream"/> via <c>await foreach</c> (single pass).</summary>
        public static GeneratedUtf8FixedWidthAsyncRecordEnumerable<TModel> ReadAsync<TModel>(
            Stream stream,
            bool leaveOpen = false,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IUtf8FixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateBufferSize(bufferSize);
            return new GeneratedUtf8FixedWidthAsyncRecordEnumerable<TModel>(
                stream, ownsStream: !leaveOpen, formatProvider, stringPool, bufferSize);
        }

        /// <summary>Reads source-generated models from a file via <c>await foreach</c> (true async I/O), reopening it for each enumeration.</summary>
        public static GeneratedUtf8FixedWidthAsyncRecordEnumerable<TModel> ReadFileAsync<TModel>(
            string path,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null,
            int bufferSize = 4096)
            where TModel : IUtf8FixedWidthModel<TModel>
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ValidateBufferSize(bufferSize);
            return new GeneratedUtf8FixedWidthAsyncRecordEnumerable<TModel>(path, formatProvider, stringPool, bufferSize);
        }

        /// <summary>
        /// Reads source-generated models from a <see cref="PipeReader"/> via <c>await foreach</c> (single
        /// pass), letting the pipe own buffering and read-ahead. Use this when the source is already a pipe
        /// (e.g. a Kestrel request body, a socket, or an upstream <c>System.IO.Pipelines</c> stage).
        /// <paramref name="leaveOpen"/> controls whether the reader is completed when iteration ends.
        /// </summary>
        public static GeneratedUtf8FixedWidthPipeRecordEnumerable<TModel> ReadAsync<TModel>(
            PipeReader reader,
            bool leaveOpen = false,
            IFormatProvider? formatProvider = null,
            StringPool? stringPool = null)
            where TModel : IUtf8FixedWidthModel<TModel>
        {
            ArgumentNullException.ThrowIfNull(reader);
            return new GeneratedUtf8FixedWidthPipeRecordEnumerable<TModel>(
                reader, completeReader: !leaveOpen, formatProvider, stringPool);
        }

        private static void ValidateBufferSize(int bufferSize)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);
        }
    }
}
