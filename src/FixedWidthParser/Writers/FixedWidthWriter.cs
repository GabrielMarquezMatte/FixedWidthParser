using System.Diagnostics.CodeAnalysis;

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
        private readonly FixedWidthLayout<TModel> _layout;
        private readonly FixedWidthWriterOptions _options;

        /// <summary>Creates a writer; <see langword="null"/> options use every default.</summary>
        [RequiresUnreferencedCode("Attribute layouts reflect over TModel's public properties and fields, which trimming may remove.")]
        [RequiresDynamicCode("Attribute layouts compile member accessors at runtime (Expression.Compile / MakeGenericType).")]
        public FixedWidthWriter(FixedWidthWriterOptions? options = null)
        {
            _layout = FixedWidthLayout.FromAttributes<TModel>();
            _layout.EnsureCanWrite();
            _options = FixedWidthWriterOptions.Validate(options);
        }

        private LayoutRecordFormatter<TModel> Formatter => new(_layout);

        /// <summary>
        /// Formats <paramref name="model"/> into <paramref name="destination"/>, without a line break.
        /// Returns <see langword="false"/> only when <paramref name="destination"/> is shorter than the line.
        /// </summary>
        public bool TryFormat(in TModel model, Span<byte> destination, out int bytesWritten)
        {
            return Formatter.TryFormat(in model, destination, _options.Context, out bytesWritten);
        }

        /// <summary>Writes one record and its line break.</summary>
        public void Write(Stream stream, in TModel model)
        {
            ArgumentNullException.ThrowIfNull(stream);
            RecordWriter.Write(stream, in model, Formatter, _options);
        }

        /// <summary>Writes every record, each followed by a line break.</summary>
        public void WriteMany(Stream stream, IEnumerable<TModel> models)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            RecordWriter.WriteMany(stream, models, Formatter, _options);
        }

        /// <summary>Writes every record, each followed by a line break, reading each one by reference.</summary>
        public void WriteMany(Stream stream, ReadOnlySpan<TModel> models)
        {
            ArgumentNullException.ThrowIfNull(stream);
            RecordWriter.WriteMany(stream, models, Formatter, _options);
        }

        /// <summary>Writes one record and its line break.</summary>
        public Task WriteAsync(Stream stream, TModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return RecordWriter.WriteAsync(stream, model, Formatter, _options, cancellationToken);
        }

        /// <summary>Writes every record, each followed by a line break.</summary>
        public Task WriteManyAsync(Stream stream, IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(models);
            return RecordWriter.WriteManyAsync(stream, models, Formatter, _options, cancellationToken);
        }
    }
}
