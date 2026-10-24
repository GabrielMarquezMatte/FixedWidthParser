using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FixedWidthParser.Writers
{
    /// <summary>
    /// Writes attribute-mapped models as fixed-width lines of bytes through the runtime
    /// (reflection-built) formatter. Batches lines into one pooled buffer, so writing many records
    /// allocates nothing per record. For source-generated models use <see cref="FixedWidth"/>.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public sealed class FixedWidthWriter<TModel> where TModel : new()
    {
        private const int BatchSize = 64 * 1024;

        private readonly FixedWidthLayout<TModel> _layout;
        private readonly FixedWidthContext _context;
        private readonly byte[] _newLine;

        /// <summary>Creates a writer; <see langword="null"/> options use every default.</summary>
        [RequiresUnreferencedCode("Attribute layouts reflect over TModel's public properties and fields, which trimming may remove.")]
        [RequiresDynamicCode("Attribute layouts compile member accessors at runtime (Expression.Compile / MakeGenericType).")]
        public FixedWidthWriter(FixedWidthWriterOptions? options = null)
        {
            _layout = FixedWidthLayout.FromAttributes<TModel>();
            _layout.EnsureCanWrite();
            options ??= FixedWidthWriterOptions.Default;
            if (options.NewLine is null)
            {
                throw new ArgumentException("NewLine must not be null.", nameof(options));
            }
            _context = options.Context;
            _newLine = _context.Encoding.GetBytes(options.NewLine);
        }

        private int LineLength => _layout.LineLength;

        private int RecordLength => LineLength + _newLine.Length;

        /// <summary>
        /// Formats <paramref name="model"/> into <paramref name="destination"/>, without a line break.
        /// Returns <see langword="false"/> only when <paramref name="destination"/> is shorter than the line.
        /// </summary>
        public bool TryFormat(in TModel model, Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < LineLength)
            {
                bytesWritten = 0;
                return false;
            }
            FormatLine(in model, destination[..LineLength]);
            bytesWritten = LineLength;
            return true;
        }

        /// <summary>Writes one record and its line break.</summary>
        [SkipLocalsInit]
        public void Write(Stream stream, in TModel model)
        {
            ArgumentNullException.ThrowIfNull(stream);
            int size = RecordLength;
            byte[]? rented = size <= 1024 ? null : ArrayPool<byte>.Shared.Rent(size);
            Span<byte> record = rented is null ? stackalloc byte[1024] : rented;
            try
            {
                stream.Write(record[..FormatRecord(in model, record)]);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>Writes every record, each followed by a line break.</summary>
        public void WriteMany(Stream stream, IEnumerable<TModel> models)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            byte[] buffer = RentBatch();
            try
            {
                int used = 0;
                foreach (var model in models)
                {
                    used = Append(stream, buffer, used, in model);
                }
                stream.Write(buffer, 0, used);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>Writes every record, each followed by a line break, reading each one by reference.</summary>
        public void WriteMany(Stream stream, ReadOnlySpan<TModel> models)
        {
            ArgumentNullException.ThrowIfNull(stream);
            byte[] buffer = RentBatch();
            try
            {
                int used = 0;
                foreach (ref readonly var model in models)
                {
                    used = Append(stream, buffer, used, in model);
                }
                stream.Write(buffer, 0, used);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>Writes one record and its line break.</summary>
        public async Task WriteAsync(Stream stream, TModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(RecordLength);
            try
            {
                int written = FormatRecord(in model, buffer);
                await stream.WriteAsync(buffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>Writes every record, each followed by a line break.</summary>
        public async Task WriteManyAsync(Stream stream, IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            byte[] buffer = RentBatch();
            try
            {
                int used = 0;
                foreach (var model in models)
                {
                    if (used + RecordLength > buffer.Length)
                    {
                        await stream.WriteAsync(buffer.AsMemory(0, used), cancellationToken).ConfigureAwait(false);
                        used = 0;
                    }
                    used += FormatRecord(in model, buffer.AsSpan(used));
                }
                await stream.WriteAsync(buffer.AsMemory(0, used), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private byte[] RentBatch()
        {
            return ArrayPool<byte>.Shared.Rent(Math.Max(BatchSize, RecordLength));
        }

        private int Append(Stream stream, byte[] buffer, int used, in TModel model)
        {
            if (used + RecordLength > buffer.Length)
            {
                stream.Write(buffer, 0, used);
                used = 0;
            }
            return used + FormatRecord(in model, buffer.AsSpan(used));
        }

        private int FormatRecord(in TModel model, Span<byte> destination)
        {
            FormatLine(in model, destination[..LineLength]);
            _newLine.CopyTo(destination[LineLength..]);
            return RecordLength;
        }

        private void FormatLine(in TModel model, Span<byte> line)
        {
            _layout.FormatLine(in model, line, in _context);
        }
    }
}
