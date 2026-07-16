using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Writers
{
    public sealed class FixedWidthWriter<TModel>
    {
        private readonly IColumnFormatter<TModel>[] _formatters;
        private readonly int _lineLength;

        public FixedWidthWriter()
        {
            var formatters = new List<IColumnFormatter<TModel>>();
            int maxLen = 0;

            ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) =>
            {
                maxLen = Math.Max(maxLen, attribute.Start + attribute.Length);
                formatters.Add(CreateFormatter(member, attribute));
            });

            _formatters = formatters.ToArray();
            _lineLength = maxLen;
        }
        private static OverflowBehavior DetermineOverflowBehavior(FixedColumnAttribute attribute, bool isString)
        {
            if (attribute.Overflow != OverflowBehavior.Default)
            {
                return attribute.Overflow;
            }

            if (isString)
            {
                return OverflowBehavior.Truncate;
            }

            return OverflowBehavior.Throw;
        }
        private static IColumnFormatter<TModel> CreateFormatter(MemberInfo member, FixedColumnAttribute attribute)
        {
            var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
            var delegateType = typeof(RefGetter<,>).MakeGenericType(typeof(TModel), memberType);
            var getter = Expression.Lambda(delegateType, access, model).Compile();
            // Resolve Default overflow per type: string truncates, numeric (incl. T?) throws.
            var overflow = DetermineOverflowBehavior(attribute, memberType == typeof(string));
            var options = new ColumnFormatOptions(attribute.Alignment, attribute.Padding, attribute.Format, overflow);

            var underlyingType = Nullable.GetUnderlyingType(memberType);
            if (underlyingType is null)
            {
                return BuildValueFormatter(member, attribute, memberType, options, getter);
            }

            // A null value writes as a blank (padding-filled) column instead of formatting
            // otherwise the underlying T formats exactly as a non-nullable column would.
            var underlyingGetter = BuildUnderlyingGetter(underlyingType, getter);
            var inner = BuildValueFormatter(member, attribute, underlyingType, options, underlyingGetter);
            var nullableFormatterType = typeof(NullableColumnFormatter<,>).MakeGenericType(typeof(TModel), underlyingType);
            return (IColumnFormatter<TModel>)Activator.CreateInstance(
                nullableFormatterType, attribute.Start, attribute.Length, options, getter, inner)!;
        }

        // Adapts a RefGetter<TModel, TUnderlying?> into a RefGetter<TModel, TUnderlying> (reads .Value
        // only ever invoked by NullableColumnFormatter after it has confirmed HasValue).
        private static Delegate BuildUnderlyingGetter(Type underlyingType, Delegate nullableGetter)
        {
            var modelParam = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var invoke = Expression.Invoke(Expression.Constant(nullableGetter), modelParam);
            var value = Expression.Property(invoke, "Value");
            var adapterType = typeof(RefGetter<,>).MakeGenericType(typeof(TModel), underlyingType);
            return Expression.Lambda(adapterType, value, modelParam).Compile();
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
                var converterInstance = Activator.CreateInstance(converterType)
                    ?? throw new InvalidOperationException($"Could not instantiate converter '{converterType}' for column '{member.Name}'.");
                var converterFormatterType = typeof(ConverterColumnFormatter<,,>).MakeGenericType(typeof(TModel), valueType, converterType);
                return (IColumnFormatter<TModel>)Activator.CreateInstance(
                    converterFormatterType, attribute.Start, attribute.Length, options, member.Name, getter, converterInstance)!;
            }

            var formatterType = valueType == typeof(string)
                ? typeof(StringColumnFormatter<>).MakeGenericType(typeof(TModel))
                : typeof(SpanFormattableColumnFormatter<,>).MakeGenericType(typeof(TModel), valueType);
            return (IColumnFormatter<TModel>)Activator.CreateInstance(
                formatterType, attribute.Start, attribute.Length, options, member.Name, getter)!;
        }

        /// <summary>
        /// Formats a single model line into the given buffer: fills it with spaces and applies
        /// each column formatter. Shared core used by every write overload.
        /// </summary>
        private void FormatLine(in TModel model, Span<char> lineBuffer, IFormatProvider? formatProvider)
        {
            lineBuffer.Fill(' ');
            foreach (ref readonly var formatter in _formatters.AsSpan())
            {
                formatter.Format(in model, lineBuffer, formatProvider);
            }
        }

        public async Task WriteAsync(StreamWriter writer, TModel model, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            var lineBuffer = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                FormatLine(in model, lineBuffer.AsSpan(0, _lineLength), formatProvider);
                await writer.WriteLineAsync(lineBuffer.AsMemory(0, _lineLength), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(lineBuffer);
            }
        }
        public async Task WriteAsync(Stream stream, TModel model, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            StreamWriter writer = new(stream, leaveOpen: true);
            await using (writer.ConfigureAwait(false))
            {
                await WriteAsync(writer, model, formatProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        public async Task WriteManyAsync(Stream stream, IEnumerable<TModel> models, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            StreamWriter writer = new(stream, leaveOpen: true);
            await using (writer.ConfigureAwait(false))
            {
                await WriteManyAsync(writer, models, formatProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        public async Task WriteManyAsync(StreamWriter writer, IEnumerable<TModel> models, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            var lineBuffer = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                foreach (var model in models)
                {
                    FormatLine(in model, lineBuffer.AsSpan(0, _lineLength), formatProvider);
                    await writer.WriteLineAsync(lineBuffer.AsMemory(0, _lineLength), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(lineBuffer);
            }
        }
        [SkipLocalsInit]
        public void Write(StreamWriter writer, in TModel model, IFormatProvider? formatProvider = null)
        {
            if (_lineLength <= 1024)
            {
                Span<char> lineBuffer = stackalloc char[_lineLength];
                FormatLine(in model, lineBuffer, formatProvider);
                writer.WriteLine(lineBuffer);
                return;
            }

            var rented = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                var lineBuffer = rented.AsSpan(0, _lineLength);
                FormatLine(in model, lineBuffer, formatProvider);
                writer.WriteLine(lineBuffer);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Writes a single model directly to a Stream.
        /// </summary>
        public void Write(Stream stream, in TModel model, IFormatProvider? formatProvider = null)
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            Write(writer, in model, formatProvider);
        }

        /// <summary>
        /// Writes a collection of models to a Stream continuously.
        /// </summary>
        public void WriteMany(Stream stream, IEnumerable<TModel> models, IFormatProvider? formatProvider = null)
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            WriteMany(writer, models, formatProvider);
        }

        /// <summary>
        /// Writes a collection of models reusing an existing StreamWriter
        /// (avoids allocating/disposing a StreamWriter per call).
        /// </summary>
        [SkipLocalsInit]
        public void WriteMany(StreamWriter writer, IEnumerable<TModel> models, IFormatProvider? formatProvider = null)
        {
            if (_lineLength <= 1024)
            {
                Span<char> lineBuffer = stackalloc char[_lineLength];
                foreach (var model in models)
                {
                    FormatLine(in model, lineBuffer, formatProvider);
                    writer.WriteLine(lineBuffer);
                }
                return;
            }

            var rented = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                var lineBuffer = rented.AsSpan(0, _lineLength);
                foreach (var model in models)
                {
                    FormatLine(in model, lineBuffer, formatProvider);
                    writer.WriteLine(lineBuffer);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Writes a contiguous collection of models to a Stream without allocating an enumerator
        /// (iterates by ref readonly, avoiding a copy of each struct).
        /// </summary>
        public void WriteMany(Stream stream, ReadOnlySpan<TModel> models, IFormatProvider? formatProvider = null)
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            WriteMany(writer, models, formatProvider);
        }

        /// <summary>
        /// Writes a contiguous collection of models reusing an existing StreamWriter, without
        /// allocating an enumerator (iterates by ref readonly, avoiding a copy of each struct).
        /// </summary>
        [SkipLocalsInit]
        public void WriteMany(StreamWriter writer, ReadOnlySpan<TModel> models, IFormatProvider? formatProvider = null)
        {
            if (_lineLength <= 1024)
            {
                Span<char> lineBuffer = stackalloc char[_lineLength];
                foreach (ref readonly var model in models)
                {
                    FormatLine(in model, lineBuffer, formatProvider);
                    writer.WriteLine(lineBuffer);
                }
                return;
            }

            var rented = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                var lineBuffer = rented.AsSpan(0, _lineLength);
                foreach (ref readonly var model in models)
                {
                    FormatLine(in model, lineBuffer, formatProvider);
                    writer.WriteLine(lineBuffer);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
