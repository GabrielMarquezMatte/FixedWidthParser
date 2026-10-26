using System.Buffers;
using System.Runtime.CompilerServices;

namespace FixedWidthParser.Writers
{
    /// <summary>
    /// Writes records, each followed by a line break, batched into one pooled buffer. The line length is
    /// not known up front (a generated model formats itself), so a record that does not fit the rest of the
    /// batch flushes it, and one that does not fit an empty batch grows it.
    /// </summary>
    internal static class RecordWriter
    {
        private const int BatchSize = 64 * 1024;
        private const int StackRecordSize = 1024;

        [SkipLocalsInit]
        public static void Write<TModel, TFormatter>(Stream stream, in TModel model, TFormatter formatter, FixedWidthWriterOptions options)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            var context = options.Context;
            Span<byte> record = stackalloc byte[StackRecordSize];
            if (TryFormatRecord(in model, formatter, record, in context, options.NewLineBytes, out int written))
            {
                stream.Write(record[..written]);
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(StackRecordSize * 2);
            try
            {
                written = FormatGrowing(in model, formatter, ref buffer, in context, options.NewLineBytes);
                stream.Write(buffer, 0, written);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void WriteMany<TModel, TFormatter>(Stream stream, IEnumerable<TModel> models, TFormatter formatter, FixedWidthWriterOptions options)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            var context = options.Context;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BatchSize);
            try
            {
                int used = 0;
                foreach (var model in models)
                {
                    used = Append(stream, in model, formatter, ref buffer, used, in context, options.NewLineBytes);
                }
                stream.Write(buffer, 0, used);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void WriteMany<TModel, TFormatter>(Stream stream, ReadOnlySpan<TModel> models, TFormatter formatter, FixedWidthWriterOptions options)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            var context = options.Context;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BatchSize);
            try
            {
                int used = 0;
                foreach (ref readonly var model in models)
                {
                    used = Append(stream, in model, formatter, ref buffer, used, in context, options.NewLineBytes);
                }
                stream.Write(buffer, 0, used);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async Task WriteAsync<TModel, TFormatter>(Stream stream, TModel model, TFormatter formatter, FixedWidthWriterOptions options, CancellationToken cancellationToken)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            var context = options.Context;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(StackRecordSize);
            try
            {
                int written = FormatGrowing(in model, formatter, ref buffer, in context, options.NewLineBytes);
                await stream.WriteAsync(buffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async Task WriteManyAsync<TModel, TFormatter>(Stream stream, IEnumerable<TModel> models, TFormatter formatter, FixedWidthWriterOptions options, CancellationToken cancellationToken)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            var context = options.Context;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BatchSize);
            try
            {
                int used = 0;
                foreach (var model in models)
                {
                    if (!TryFormatRecord(in model, formatter, buffer.AsSpan(used), in context, options.NewLineBytes, out int written))
                    {
                        await stream.WriteAsync(buffer.AsMemory(0, used), cancellationToken).ConfigureAwait(false);
                        used = 0;
                        written = FormatGrowing(in model, formatter, ref buffer, in context, options.NewLineBytes);
                    }
                    used += written;
                }
                await stream.WriteAsync(buffer.AsMemory(0, used), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static int Append<TModel, TFormatter>(
            Stream stream, in TModel model, TFormatter formatter, ref byte[] buffer, int used, in FixedWidthContext context, byte[] newLine)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            if (TryFormatRecord(in model, formatter, buffer.AsSpan(used), in context, newLine, out int written))
            {
                return used + written;
            }
            stream.Write(buffer, 0, used);
            return FormatGrowing(in model, formatter, ref buffer, in context, newLine);
        }

        // Formats into the whole of buffer, replacing it with one twice the size until the record fits.
        // The new buffer is rented before the old one is returned, so the caller's finally never returns a buffer twice.
        private static int FormatGrowing<TModel, TFormatter>(in TModel model, TFormatter formatter, ref byte[] buffer, in FixedWidthContext context, byte[] newLine)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            while (true)
            {
                if (TryFormatRecord(in model, formatter, buffer, in context, newLine, out int written))
                {
                    return written;
                }
                byte[] larger = ArrayPool<byte>.Shared.Rent(checked(buffer.Length * 2));
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = larger;
            }
        }

        /// <summary>
        /// Formats a record and its line break into <paramref name="destination"/>; false when they do not fit. A record
        /// with no columns and an empty line break fits in zero bytes, so the count alone cannot tell.
        /// </summary>
        private static bool TryFormatRecord<TModel, TFormatter>(
            in TModel model, TFormatter formatter, Span<byte> destination, in FixedWidthContext context, byte[] newLine, out int written)
            where TFormatter : struct, IRecordFormatter<TModel>
        {
            if (!formatter.TryFormat(in model, destination, in context, out int record) || destination.Length - record < newLine.Length)
            {
                written = 0;
                return false;
            }
            newLine.CopyTo(destination[record..]);
            written = record + newLine.Length;
            return true;
        }
    }
}
