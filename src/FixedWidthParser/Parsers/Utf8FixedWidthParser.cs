using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Processors;

namespace FixedWidthParser.Parsers
{
    /// <summary>
    /// UTF-8 counterpart of <see cref="FixedWidthParser{TModel}"/>: parses a fixed-width line directly
    /// from raw <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> (UTF-8), skipping the
    /// UTF-8 → UTF-16 transcode that the <c>char</c> parser pays via a <see cref="StreamReader"/>.
    /// <para>
    /// <b>Column offsets are measured in bytes.</b> This is exact for the single-byte (ASCII) data
    /// that fixed-width/flat files almost always use; for content with multi-byte UTF-8 (e.g. accented
    /// characters) a byte offset is not the same as a character offset, so this parser is intended for
    /// single-byte payloads. Non-string columns parse via <see cref="Utf8ColumnParserRegistry"/> /
    /// <see cref="IUtf8SpanParsable{TSelf}"/>; <c>string</c> columns are UTF-8 decoded (no pooling).
    /// </para>
    /// </summary>
    public sealed class Utf8FixedWidthParser<TModel> where TModel : new(), allows ref struct
    {
        private readonly record struct ColumnParserInfo(int Start, int Length, Utf8ColumnParser<TModel> Parse);
        private static readonly Func<TModel> _modelFactory;
        private static readonly ColumnParserInfo[] _processors;
        private static readonly int _requiredLineLength;
        private static readonly ExceptionDispatchInfo? _buildError;

        // The build is static (once per type). Any layout/configuration error is captured and
        // rethrown from the instance constructor, so the caller gets a clean exception instead of
        // a TypeInitializationException on first use. Mirrors FixedWidthParser<TModel>.
        static Utf8FixedWidthParser()
        {
            try
            {
                _modelFactory = BuildModelFactory();
                _processors = BuildProcessors();
                _requiredLineLength = ComputeRequiredLineLength(_processors);
                _buildError = null;
            }
            catch (Exception ex)
            {
                _modelFactory = null!;
                _processors = [];
                _requiredLineLength = 0;
                _buildError = ExceptionDispatchInfo.Capture(ex);
            }
        }

        public Utf8FixedWidthParser() => _buildError?.Throw();

        private static Func<TModel> BuildModelFactory()
        {
            var ctor = typeof(TModel).GetConstructor(Type.EmptyTypes);
            if (ctor is null)
            {
                throw new InvalidOperationException($"Type {typeof(TModel).FullName} must have a parameterless constructor.");
            }
            var lambda = Expression.Lambda<Func<TModel>>(Expression.New(ctor));
            return lambda.Compile();
        }

        private static ColumnParserInfo[] BuildProcessors()
        {
            var processors = new List<ColumnParserInfo>();
            ModelColumns.ForEachColumn(typeof(TModel), (member, attribute) =>
                processors.Add(new(attribute.Start, attribute.Length, CreateColumnParser(member))));
            return processors.ToArray();
        }

        private static int ComputeRequiredLineLength(ReadOnlySpan<ColumnParserInfo> processors)
        {
            int required = 0;
            foreach (var (Start, Length, _) in processors)
            {
                int end = Start + Length;
                if (end > required)
                {
                    required = end;
                }
            }
            return required;
        }

        private static Utf8ColumnParser<TModel> CreateColumnParser(MemberInfo member)
        {
            var (memberType, model, access) = ModelColumns.MemberAccess(typeof(TModel), member);
            var value = Expression.Parameter(memberType, "value");
            var actionType = typeof(RefAction<,>).MakeGenericType(typeof(TModel), memberType);
            var setter = Expression.Lambda(actionType, Expression.Assign(access, value), model, value).Compile();
            return Utf8ColumnParserFactory.Create<TModel>(memberType, setter);
        }

        /// <summary>
        /// Parses a single UTF-8 fixed-width line into <paramref name="model"/>. Returns
        /// <see langword="false"/> when the line is shorter (in bytes) than the configured layout or
        /// when a non-string column fails to parse.
        /// </summary>
        public bool TryParse(ReadOnlySpan<byte> line, IFormatProvider? formatProvider, out TModel model)
        {
            if (line.Length < _requiredLineLength)
            {
                model = default!;
                return false;
            }

            model = _modelFactory();
            foreach (ref readonly var processor in _processors.AsSpan())
            {
                var column = line.Slice(processor.Start, processor.Length);
                if (!processor.Parse(column, formatProvider, ref model))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
