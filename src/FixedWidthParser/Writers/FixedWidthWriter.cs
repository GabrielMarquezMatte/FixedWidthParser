using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
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
            var properties = typeof(TModel).GetProperties();
            var fields = typeof(TModel).GetFields();
            List<IColumnFormatter<TModel>> formatters = new(properties.Length + fields.Length);
            int maxLen = 0;
            foreach (var prop in properties)
            {
                AddFormatter(prop, formatters, ref maxLen);
            }
            foreach (var field in fields)
            {
                AddFormatter(field, formatters, ref maxLen);
            }
            _formatters = formatters.ToArray();
            _lineLength = maxLen;
        }

        private static void AddFormatter(MemberInfo member, List<IColumnFormatter<TModel>> formatters, ref int maxLen)
        {
            var attribute = member.GetCustomAttribute<FixedColumnAttribute>();
            if (attribute is null) return;
            maxLen = Math.Max(maxLen, attribute.Start + attribute.Length);
            formatters.Add(CreateFormatter(member, attribute));
        }

        private static IColumnFormatter<TModel> CreateFormatter(MemberInfo member, FixedColumnAttribute attribute)
        {
            var targetExpr = Expression.Parameter(typeof(TModel).MakeByRefType(), "model");
            var (memberType, memberExpr) = member switch
            {
                PropertyInfo p => (p.PropertyType, Expression.Property(targetExpr, p)),
                FieldInfo f => (f.FieldType, Expression.Field(targetExpr, f)),
                _ => throw new ArgumentException($"Membro não suportado: {member.GetType().Name}", nameof(member))
            };
            var delegateType = typeof(RefGetter<,>).MakeGenericType(typeof(TModel), memberType);
            var getter = Expression.Lambda(delegateType, memberExpr, targetExpr).Compile();
            bool isString = memberType == typeof(string);
            // Resolve o overflow Default por tipo: string trunca, numéricos lançam.
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
        /// Formata uma única linha do modelo no buffer informado: preenche com espaços e
        /// aplica cada formatter de coluna. Núcleo compartilhado por todos os overloads de escrita.
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
        public void Write(StreamWriter writer, in TModel model, IFormatProvider? formatProvider)
        {
            Span<char> lineBuffer = _lineLength <= 1024 ? stackalloc char[_lineLength] : new char[_lineLength];
            FormatLine(in model, lineBuffer, formatProvider);
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
            WriteMany(writer, models, formatProvider);
        }

        /// <summary>
        /// Escreve uma coleção de modelos reaproveitando um StreamWriter já existente
        /// (evita alocar/descartar um StreamWriter por chamada).
        /// </summary>
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
        /// Escreve uma coleção contígua de modelos sobre uma Stream, sem alocar enumerador
        /// (itera por ref readonly, evitando cópia de cada struct).
        /// </summary>
        public void WriteMany(Stream stream, ReadOnlySpan<TModel> models, IFormatProvider? formatProvider = null)
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            WriteMany(writer, models, formatProvider);
        }

        /// <summary>
        /// Escreve uma coleção contígua de modelos reaproveitando um StreamWriter existente,
        /// sem alocar enumerador (itera por ref readonly, evitando cópia de cada struct).
        /// </summary>
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
