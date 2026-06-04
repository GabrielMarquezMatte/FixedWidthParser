using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;

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

        private static IColumnFormatter<TModel> CreateFormatter(MemberInfo member, FixedColumnAttribute attribute)
        {
            var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
            var delegateType = typeof(RefGetter<,>).MakeGenericType(typeof(TModel), memberType);
            var getter = Expression.Lambda(delegateType, access, model).Compile();
            bool isString = memberType == typeof(string);
            // Resolve Default overflow per type: string truncates, numeric throws.
            var overflow = attribute.Overflow == OverflowBehavior.Default
                ? (isString ? OverflowBehavior.Truncate : OverflowBehavior.Throw)
                : attribute.Overflow;
            var options = new ColumnFormatOptions(attribute.Alignment, attribute.Padding, attribute.Format, overflow);
            var formatterType = isString
                ? typeof(StringColumnFormatter<>).MakeGenericType(typeof(TModel))
                : typeof(SpanFormattableColumnFormatter<,>).MakeGenericType(typeof(TModel), memberType);
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
                await writer.WriteLineAsync(lineBuffer.AsMemory(0, _lineLength), cancellationToken);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(lineBuffer);
            }
        }
        public async Task WriteAsync(Stream stream, TModel model, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            using StreamWriter writer = new(stream, leaveOpen: true);
            await WriteAsync(writer, model, formatProvider, cancellationToken);
        }
        public async Task WriteManyAsync(Stream stream, IEnumerable<TModel> models, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            using StreamWriter writer = new(stream, leaveOpen: true);
            await WriteManyAsync(writer, models, formatProvider, cancellationToken);
        }
        public async Task WriteManyAsync(StreamWriter writer, IEnumerable<TModel> models, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            var lineBuffer = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                foreach (var model in models)
                {
                    FormatLine(in model, lineBuffer.AsSpan(0, _lineLength), formatProvider);
                    await writer.WriteLineAsync(lineBuffer.AsMemory(0, _lineLength), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(lineBuffer);
            }
        }
        [SkipLocalsInit]
        public void Write(StreamWriter writer, in TModel model, IFormatProvider? formatProvider)
        {
            Span<char> lineBuffer = _lineLength <= 1024 ? stackalloc char[_lineLength] : new char[_lineLength];
            FormatLine(in model, lineBuffer, formatProvider);
            writer.WriteLine(lineBuffer);
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
            Span<char> lineBuffer = _lineLength <= 1024 ? stackalloc char[_lineLength] : new char[_lineLength];
            foreach (var model in models)
            {
                FormatLine(in model, lineBuffer, formatProvider);
                writer.WriteLine(lineBuffer);
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
            Span<char> lineBuffer = _lineLength <= 1024 ? stackalloc char[_lineLength] : new char[_lineLength];
            foreach (ref readonly var model in models)
            {
                FormatLine(in model, lineBuffer, formatProvider);
                writer.WriteLine(lineBuffer);
            }
        }
    }
}
