using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using Benchmarks.Attributes;
using Benchmarks.Formatters;

namespace Benchmarks.Writers
{
    public sealed class FixedWidthWriter<TModel>
    {
        private readonly IColumnFormatter<TModel>[] _formatters;
        private readonly int _lineLength;

        public FixedWidthWriter()
        {
            var properties = typeof(TModel).GetProperties();
            List<IColumnFormatter<TModel>> formatters = new(properties.Length);
            int maxLen = 0;
            foreach (var prop in properties)
            {
                var attribute = prop.GetCustomAttribute<FixedColumnAttribute>();
                if (attribute is null) continue;
                maxLen = Math.Max(maxLen, attribute.Start + attribute.Length);
                var targetExpr = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
                var propExpr = Expression.Property(targetExpr, prop);
                var delegateType = typeof(RefGetter<,>).MakeGenericType(typeof(TModel), prop.PropertyType);
                var getter = Expression.Lambda(delegateType, propExpr, targetExpr).Compile();
                Type formatterType = prop.PropertyType == typeof(string)
                    ? typeof(StringColumnFormatter<>).MakeGenericType(typeof(TModel))
                    : typeof(SpanFormattableColumnFormatter<,>).MakeGenericType(typeof(TModel), prop.PropertyType);
                var formatter = Activator.CreateInstance(formatterType, attribute.Start, attribute.Length, getter)!;
                formatters.Add((IColumnFormatter<TModel>)formatter);
            }
            _formatters = formatters.ToArray();
            _lineLength = maxLen;
        }
        public async Task WriteAsync(StreamWriter writer, TModel model, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            var lineBuffer = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                lineBuffer.AsSpan(0, _lineLength).Fill(' ');
                foreach (ref readonly var formatter in _formatters.AsSpan())
                {
                    formatter.Format(in model, lineBuffer, formatProvider);
                }
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
            var lineBuffer = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                foreach (var model in models)
                {
                    var modelRef = model;
                    lineBuffer.AsSpan(0, _lineLength).Fill(' ');
                    foreach (ref readonly var formatter in _formatters.AsSpan())
                    {
                        formatter.Format(in modelRef, lineBuffer, formatProvider);
                    }
                    await writer.WriteLineAsync(lineBuffer.AsMemory(0, _lineLength), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(lineBuffer);
            }
        }
        public async Task WriteManyAsync(StreamWriter writer, IEnumerable<TModel> models, IFormatProvider? formatProvider = null, CancellationToken cancellationToken = default)
        {
            var lineBuffer = ArrayPool<char>.Shared.Rent(_lineLength);
            try
            {
                foreach (var model in models)
                {
                    var modelRef = model;
                    lineBuffer.AsSpan(0, _lineLength).Fill(' ');
                    foreach (ref readonly var formatter in _formatters.AsSpan())
                    {
                        formatter.Format(in modelRef, lineBuffer, formatProvider);
                    }
                    await writer.WriteLineAsync(lineBuffer.AsMemory(0, _lineLength), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(lineBuffer);
            }
        }
        public void Write(StreamWriter writer, in TModel model, IFormatProvider? formatProvider)
        {
            Span<char> lineBuffer = _lineLength <= 1024 ? stackalloc char[_lineLength] : new char[_lineLength];
            lineBuffer.Fill(' ');
            foreach (ref readonly var formatter in _formatters.AsSpan())
            {
                formatter.Format(in model, lineBuffer, formatProvider);
            }
            writer.WriteLine(lineBuffer);
        }

        /// <summary>
        /// Escreve um único modelo diretamente em uma Stream.
        /// </summary>
        public void Write(Stream stream, in TModel model, IFormatProvider? formatProvider = null)
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            Write(writer, in model, formatProvider);
        }

        /// <summary>
        /// Escreve uma coleção de modelos em uma Stream de forma contínua.
        /// </summary>
        public void WriteMany(Stream stream, IEnumerable<TModel> models, IFormatProvider? formatProvider = null)
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            Span<char> lineBuffer = _lineLength <= 1024 ? stackalloc char[_lineLength] : new char[_lineLength];
            foreach (var model in models)
            {
                var modelRef = model;
                lineBuffer.Fill(' ');
                foreach (ref readonly var formatter in _formatters.AsSpan())
                {
                    formatter.Format(in modelRef, lineBuffer, formatProvider);
                }
                writer.WriteLine(lineBuffer);
            }
        }
    }
}