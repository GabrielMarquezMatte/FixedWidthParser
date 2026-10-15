using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Writers
{
    /// <summary>
    /// Writes attribute-mapped models as fixed-width lines of bytes through the runtime
    /// (reflection-built) formatter. Batches lines into one pooled buffer, so writing many records
    /// allocates nothing per record. For source-generated models use <see cref="FixedWidth.TryFormat{TModel}"/>.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    public sealed class FixedWidthWriter<TModel>
    {
        private const int BatchSize = 64 * 1024;

        private static readonly IColumnFormatter<TModel>[] _formatters;
        private static readonly int _lineLength;
        private static readonly ExceptionDispatchInfo? _buildError;

#pragma warning disable CA1810, S3963 // The try/catch needs a static constructor.
        static FixedWidthWriter()
        {
            try
            {
                var formatters = new List<IColumnFormatter<TModel>>();
                int lineLength = 0;
                ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) =>
                {
                    lineLength = Math.Max(lineLength, attribute.Start + attribute.Length);
                    formatters.Add(CreateFormatter(member, attribute));
                });
                _formatters = [.. formatters];
                _lineLength = lineLength;
                _buildError = null;
            }
#pragma warning disable CA1031 // Rethrown from the instance constructor.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _formatters = [];
                _lineLength = 0;
                _buildError = ExceptionDispatchInfo.Capture(ex);
            }
        }
#pragma warning restore CA1810, S3963

        private readonly FixedWidthContext _context;
        private readonly byte[] _newLine;

        /// <summary>Creates a writer; <see langword="null"/> options use every default.</summary>
        public FixedWidthWriter(FixedWidthWriterOptions? options = null)
        {
            _buildError?.Throw();
            options ??= FixedWidthWriterOptions.Default;
            if (options.NewLine is null)
            {
                throw new ArgumentException("NewLine must not be null.", nameof(options));
            }
            _context = options.Context;
            _newLine = _context.Encoding.GetBytes(options.NewLine);
        }

        private int RecordLength => _lineLength + _newLine.Length;

        /// <summary>
        /// Formats <paramref name="model"/> into <paramref name="destination"/>, without a line break.
        /// Returns <see langword="false"/> only when <paramref name="destination"/> is shorter than the line.
        /// </summary>
        public bool TryFormat(in TModel model, Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < _lineLength)
            {
                bytesWritten = 0;
                return false;
            }
            FormatLine(in model, destination[.._lineLength]);
            bytesWritten = _lineLength;
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
            FormatLine(in model, destination[.._lineLength]);
            _newLine.CopyTo(destination[_lineLength..]);
            return RecordLength;
        }

        private void FormatLine(in TModel model, Span<byte> line)
        {
            line.Fill((byte)' ');
            foreach (var formatter in _formatters)
            {
                formatter.Format(in model, line, in _context);
            }
        }

        private static IColumnFormatter<TModel> CreateFormatter(MemberInfo member, FixedColumnAttribute attribute)
        {
            var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
            FixedWidthRuntime.ToAsciiByte(attribute.Padding, member.Name, nameof(FixedColumnAttribute.Padding));
            byte fill = FixedWidthRuntime.ToAsciiByte(attribute.TrimChar, member.Name, nameof(FixedColumnAttribute.TrimChar));

            if (attribute.Overflow == OverflowBehavior.Truncate && memberType != typeof(string))
            {
                throw new InvalidOperationException(
                    $"OverflowBehavior.Truncate is not supported for non-string column '{member.Name}' of type '{memberType.Name}'.");
            }

            var getter = Expression.Lambda(typeof(RefGetter<,>).MakeGenericType(typeof(TModel), memberType), access, model).Compile();
            // Default overflow resolves per type: string truncates, everything else throws.
            var overflow = attribute.Overflow;
            if (overflow == OverflowBehavior.Default)
            {
                overflow = memberType == typeof(string) ? OverflowBehavior.Truncate : OverflowBehavior.Throw;
            }
            var options = new ColumnFormatOptions(attribute.Alignment, attribute.Padding, attribute.Format, overflow);

            var underlyingType = Nullable.GetUnderlyingType(memberType);
            if (underlyingType is null)
            {
                return BuildValueFormatter(member, attribute, memberType, options, getter);
            }

            var inner = BuildValueFormatter(member, attribute, underlyingType, options, BuildUnderlyingGetter(underlyingType, getter));
            return (IColumnFormatter<TModel>)Activator.CreateInstance(
                typeof(NullableColumnFormatter<,>).MakeGenericType(typeof(TModel), underlyingType),
                attribute.Start, attribute.Length, fill, getter, inner)!;
        }

        // Adapts a RefGetter<TModel, T?> into a RefGetter<TModel, T>; only called after HasValue is confirmed.
        private static Delegate BuildUnderlyingGetter(Type underlyingType, Delegate nullableGetter)
        {
            var modelParam = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var value = Expression.Property(Expression.Invoke(Expression.Constant(nullableGetter), modelParam), "Value");
            return Expression.Lambda(typeof(RefGetter<,>).MakeGenericType(typeof(TModel), underlyingType), value, modelParam).Compile();
        }

        private static IColumnFormatter<TModel> BuildValueFormatter(
            MemberInfo member, FixedColumnAttribute attribute, Type valueType, ColumnFormatOptions options, Delegate getter)
        {
            if (attribute.Converter is { } converterType)
            {
                var requiredInterface = typeof(IFixedWidthConverter<>).MakeGenericType(valueType);
                if (!requiredInterface.IsAssignableFrom(converterType))
                {
                    throw new InvalidOperationException(
                        $"Converter '{converterType}' for column '{member.Name}' must implement '{requiredInterface}'.");
                }
                var converter = Activator.CreateInstance(converterType)
                    ?? throw new InvalidOperationException($"Could not instantiate converter '{converterType}' for column '{member.Name}'.");
                return (IColumnFormatter<TModel>)Activator.CreateInstance(
                    typeof(ConverterColumnFormatter<,,>).MakeGenericType(typeof(TModel), valueType, converterType),
                    attribute.Start, attribute.Length, options, member.Name, getter, converter)!;
            }

            if (valueType == typeof(string))
            {
                return new StringColumnFormatter<TModel>(attribute.Start, attribute.Length, options, member.Name, (RefGetter<TModel, string>)getter);
            }

            if (!typeof(ISpanFormattable).IsAssignableFrom(valueType))
            {
                throw new InvalidOperationException(
                    $"Column '{member.Name}' has type '{valueType}', which is not string or ISpanFormattable; set a Converter for it.");
            }
            return (IColumnFormatter<TModel>)Activator.CreateInstance(
                typeof(SpanFormattableColumnFormatter<,>).MakeGenericType(typeof(TModel), valueType),
                attribute.Start, attribute.Length, options, member.Name, getter)!;
        }
    }
}
