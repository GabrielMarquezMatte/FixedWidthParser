using System.Runtime.InteropServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;
using FixedWidthParser.Parsers;
using FixedWidthParser.Processors;

namespace FixedWidthParser
{
    /// <summary>
    /// Describes a <see cref="FixedWidthLayout{TModel}"/> column by column, for
    /// <see cref="FixedWidthLayout.Build{TModel}"/>. Each column binds to the model through a getter, used
    /// to write, and a setter, used to read; either may be <see langword="null"/>, which leaves the layout
    /// unable to write or to read. No reflection: trimming and AOT safe.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public sealed class FixedWidthLayoutBuilder<TModel>
        where TModel : allows ref struct
    {
        private readonly List<(int Start, int Length, string Name)> _columns = [];
        private readonly List<ColumnParserInfo<TModel>> _parsers = [];
        private readonly List<IColumnFormatter<TModel>> _formatters = [];
        private bool _missingSetter;
        private bool _missingGetter;
        private Func<TModel>? _factory;

        internal FixedWidthLayoutBuilder()
        {
        }

        /// <summary>Adds a <see cref="string"/> column, decoded with the configured encoding.</summary>
        /// <param name="name">The column's name in error messages.</param>
        /// <param name="start">The 0-based offset of the column, in bytes.</param>
        /// <param name="length">The width of the column, in bytes.</param>
        /// <param name="get">Reads the value to write; <see langword="null"/> for a layout that only reads.</param>
        /// <param name="set">Assigns the parsed value; <see langword="null"/> for a layout that only writes.</param>
        /// <param name="options">Trimming, alignment, padding, format and overflow. <see langword="null"/> uses every default.</param>
        /// <returns>This builder.</returns>
        public FixedWidthLayoutBuilder<TModel> Column(
            string name, int start, int length, ColumnGetter<TModel, string>? get, ColumnSetter<TModel, string>? set, FixedWidthColumnOptions? options = null)
        {
            var column = Begin(name, start, length, typeof(string), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForString(set, column.Trim, column.TrimMode);
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                formatter = new StringColumnFormatter<TModel>(start, length, column.FormatOptions, name, get);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>
        /// Adds a column of a type that parses from and formats to UTF-8: the numeric types, <see cref="Guid"/>
        /// and the like. A date or time with a <see cref="FixedWidthColumnOptions.Format"/> parses exactly
        /// with that format.
        /// </summary>
        /// <inheritdoc cref="Column(string, int, int, ColumnGetter{TModel, string}?, ColumnSetter{TModel, string}?, FixedWidthColumnOptions?)"/>
        public FixedWidthLayoutBuilder<TModel> Column<TValue>(
            string name, int start, int length, ColumnGetter<TModel, TValue>? get, ColumnSetter<TModel, TValue>? set, FixedWidthColumnOptions? options = null)
            where TValue : IUtf8SpanParsable<TValue>, IUtf8SpanFormattable, ISpanFormattable
        {
            var column = Begin(name, start, length, typeof(TValue), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForUtf8Parsable(set, column.Trim, column.TrimMode, column.Format);
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                formatter = new Utf8SpanFormattableColumnFormatter<TModel, TValue>(start, length, column.FormatOptions, name, get);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>
        /// Adds a column of a type that parses only from characters: the column is decoded with the
        /// configured encoding first. A date or time with a <see cref="FixedWidthColumnOptions.Format"/>
        /// parses exactly with that format.
        /// </summary>
        /// <inheritdoc cref="Column(string, int, int, ColumnGetter{TModel, string}?, ColumnSetter{TModel, string}?, FixedWidthColumnOptions?)"/>
        public FixedWidthLayoutBuilder<TModel> DecodedColumn<TValue>(
            string name, int start, int length, ColumnGetter<TModel, TValue>? get, ColumnSetter<TModel, TValue>? set, FixedWidthColumnOptions? options = null)
            where TValue : ISpanParsable<TValue>, ISpanFormattable
        {
            var column = Begin(name, start, length, typeof(TValue), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForDecodedParsable(set, column.Trim, column.TrimMode, column.Format);
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                formatter = new SpanFormattableColumnFormatter<TModel, TValue>(start, length, column.FormatOptions, name, get);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>
        /// Adds a <see cref="Nullable{TValue}"/> column: a column holding only its trim character reads as
        /// <see langword="null"/>, and <see langword="null"/> writes the column filled with it.
        /// </summary>
        /// <inheritdoc cref="Column(string, int, int, ColumnGetter{TModel, string}?, ColumnSetter{TModel, string}?, FixedWidthColumnOptions?)"/>
        public FixedWidthLayoutBuilder<TModel> NullableColumn<TValue>(
            string name, int start, int length, ColumnGetter<TModel, TValue?>? get, ColumnSetter<TModel, TValue?>? set, FixedWidthColumnOptions? options = null)
            where TValue : struct, IUtf8SpanParsable<TValue>, IUtf8SpanFormattable, ISpanFormattable
        {
            var column = Begin(name, start, length, typeof(TValue?), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForNullable(set, column.Trim,
                    adapted => ColumnParserFactory.ForUtf8Parsable(adapted, column.Trim, column.TrimMode, column.Format));
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                var inner = new Utf8SpanFormattableColumnFormatter<TModel, TValue>(
                    start, length, column.FormatOptions, name, (in TModel model) => get(in model).GetValueOrDefault());
                formatter = new NullableColumnFormatter<TModel, TValue>(start, length, column.Trim, get, inner);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>
        /// Adds a <see cref="Nullable{TValue}"/> column of a type that parses only from characters, such as a
        /// date or time: blank reads as <see langword="null"/>, and <see langword="null"/> writes the column
        /// filled with its trim character. A <see cref="FixedWidthColumnOptions.Format"/> parses a date or
        /// time exactly.
        /// </summary>
        /// <inheritdoc cref="Column(string, int, int, ColumnGetter{TModel, string}?, ColumnSetter{TModel, string}?, FixedWidthColumnOptions?)"/>
        public FixedWidthLayoutBuilder<TModel> NullableDecodedColumn<TValue>(
            string name, int start, int length, ColumnGetter<TModel, TValue?>? get, ColumnSetter<TModel, TValue?>? set, FixedWidthColumnOptions? options = null)
            where TValue : struct, ISpanParsable<TValue>, ISpanFormattable
        {
            var column = Begin(name, start, length, typeof(TValue?), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForNullable(set, column.Trim,
                    adapted => ColumnParserFactory.ForDecodedParsable(adapted, column.Trim, column.TrimMode, column.Format));
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                var inner = new SpanFormattableColumnFormatter<TModel, TValue>(
                    start, length, column.FormatOptions, name, (in TModel model) => get(in model).GetValueOrDefault());
                formatter = new NullableColumnFormatter<TModel, TValue>(start, length, column.Trim, get, inner);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>
        /// Adds a <see cref="Nullable{TValue}"/> column parsed and formatted by <paramref name="converter"/>:
        /// blank reads as <see langword="null"/> without calling the converter, and <see langword="null"/>
        /// writes the column filled with its trim character.
        /// </summary>
        /// <inheritdoc cref="ConvertedColumn{TValue}(string, int, int, ColumnGetter{TModel, TValue}?, ColumnSetter{TModel, TValue}?, IFixedWidthConverter{TValue}, FixedWidthColumnOptions?)"/>
        public FixedWidthLayoutBuilder<TModel> NullableConvertedColumn<TValue>(
            string name, int start, int length, ColumnGetter<TModel, TValue?>? get, ColumnSetter<TModel, TValue?>? set,
            IFixedWidthConverter<TValue> converter, FixedWidthColumnOptions? options = null)
            where TValue : struct
        {
            ArgumentNullException.ThrowIfNull(converter);
            var column = Begin(name, start, length, typeof(TValue?), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForNullable(set, column.Trim,
                    adapted => ColumnParserFactory.ForConverter(adapted, converter, column.Trim, column.TrimMode));
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                var inner = new ConverterColumnFormatter<TModel, TValue, IFixedWidthConverter<TValue>>(
                    start, length, column.FormatOptions, name, (in TModel model) => get(in model).GetValueOrDefault(), converter);
                formatter = new NullableColumnFormatter<TModel, TValue>(start, length, column.Trim, get, inner);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>Adds a column parsed and formatted by <paramref name="converter"/>.</summary>
        /// <param name="name">The column's name in error messages.</param>
        /// <param name="start">The 0-based offset of the column, in bytes.</param>
        /// <param name="length">The width of the column, in bytes.</param>
        /// <param name="get">Reads the value to write; <see langword="null"/> for a layout that only reads.</param>
        /// <param name="set">Assigns the parsed value; <see langword="null"/> for a layout that only writes.</param>
        /// <param name="converter">Parses and formats the column's value; shared by every line, and by every thread reading in parallel.</param>
        /// <param name="options">Trimming, alignment, padding and overflow. <see langword="null"/> uses every default.</param>
        /// <returns>This builder.</returns>
        public FixedWidthLayoutBuilder<TModel> ConvertedColumn<TValue>(
            string name, int start, int length, ColumnGetter<TModel, TValue>? get, ColumnSetter<TModel, TValue>? set,
            IFixedWidthConverter<TValue> converter, FixedWidthColumnOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(converter);
            var column = Begin(name, start, length, typeof(TValue), options);
            ColumnParser<TModel>? parser = null;
            if (set is not null)
            {
                parser = ColumnParserFactory.ForConverter(set, converter, column.Trim, column.TrimMode);
            }
            IColumnFormatter<TModel>? formatter = null;
            if (get is not null)
            {
                formatter = new ConverterColumnFormatter<TModel, TValue, IFixedWidthConverter<TValue>>(start, length, column.FormatOptions, name, get, converter);
            }
            return Add(column, parser, formatter);
        }

        /// <summary>
        /// Sets how a read creates the model before its columns are assigned. Required to read a reference
        /// type; a value type without one starts from <see langword="default"/>.
        /// </summary>
        /// <param name="factory">Returns a new model for each line.</param>
        /// <returns>This builder.</returns>
        public FixedWidthLayoutBuilder<TModel> Factory(Func<TModel> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _factory = factory;
            return this;
        }

        internal FixedWidthLayout<TModel> Build()
        {
            if (_columns.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The layout for {typeof(TModel).Name} has no columns; a fixed-width layout must define at least one column.");
            }
            ColumnLayoutValidator.Validate(CollectionsMarshal.AsSpan(_columns), typeof(TModel).Name);

            ColumnParserInfo<TModel>[]? parsers = null;
            Func<TModel>? factory = null;
            if (!_missingSetter)
            {
                parsers = [.. _parsers];
                factory = ResolveFactory();
            }
            IColumnFormatter<TModel>[]? formatters = null;
            if (!_missingGetter)
            {
                formatters = [.. _formatters];
            }
            return new(parsers, factory, formatters, _columns.Max(static column => column.Start + column.Length));
        }

        private Func<TModel> ResolveFactory()
        {
            if (_factory is not null)
            {
                return _factory;
            }
            if (typeof(TModel).IsValueType)
            {
                return static () => default!;
            }
            throw new InvalidOperationException(
                $"The layout for {typeof(TModel).Name} has no factory: {typeof(TModel).Name} is a reference type, so reading needs a new instance per line. Call Factory(...).");
        }

        private PendingColumn Begin(string name, int start, int length, Type valueType, FixedWidthColumnOptions? options)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            options ??= FixedWidthColumnOptions.Default;
            var formatOptions = FormatterBuilder.ResolveOptions(name, valueType, options.Alignment, options.Padding, options.Format, options.Overflow);
            byte trim = FixedWidthRuntime.ToAsciiByte(options.TrimChar, name, nameof(FixedWidthColumnOptions.TrimChar));
            _columns.Add((start, length, name));
            return new(name, start, length, trim, options.TrimMode, options.Format, formatOptions);
        }

        private FixedWidthLayoutBuilder<TModel> Add(in PendingColumn column, ColumnParser<TModel>? parser, IColumnFormatter<TModel>? formatter)
        {
            _missingSetter |= parser is null;
            _missingGetter |= formatter is null;
            if (parser is not null)
            {
                _parsers.Add(new(column.Start, column.Length, column.Name, parser));
            }
            if (formatter is not null)
            {
                _formatters.Add(formatter);
            }
            return this;
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct PendingColumn(
            string Name, int Start, int Length, byte Trim, TrimMode TrimMode, string? Format, ColumnFormatOptions FormatOptions);
    }
}
