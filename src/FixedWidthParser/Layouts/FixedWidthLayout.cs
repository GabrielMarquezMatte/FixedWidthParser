using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using FixedWidthParser.Attributes;
using FixedWidthParser.Formatters;
using FixedWidthParser.Parsers;

namespace FixedWidthParser
{
    /// <summary>
    /// How <typeparamref name="TModel"/> maps to the columns of a fixed-width line, for reading and for
    /// writing. Immutable and safe to share across threads. Create one with <see cref="FixedWidthLayout"/>
    /// and pass it to the <see cref="FixedWidth"/> methods.
    /// <para><b>Column positions are bytes.</b></para>
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public sealed class FixedWidthLayout<TModel>
        where TModel : allows ref struct
    {
        private readonly ColumnParserInfo<TModel>[]? _parsers;
        private readonly Func<TModel>? _factory;
        private readonly IColumnFormatter<TModel>[]? _formatters;
        private readonly ExceptionDispatchInfo? _readError;
        private readonly ExceptionDispatchInfo? _writeError;

        internal FixedWidthLayout(ColumnParserInfo<TModel>[]? parsers, Func<TModel>? factory, IColumnFormatter<TModel>[]? formatters, int lineLength,
            ExceptionDispatchInfo? readError = null, ExceptionDispatchInfo? writeError = null)
        {
            _parsers = parsers;
            _factory = factory;
            _formatters = formatters;
            LineLength = lineLength;
            _readError = readError;
            _writeError = writeError;
        }

        /// <summary>The end of the last column: the length of a written line, and the shortest line that parses.</summary>
        internal int LineLength { get; }

        internal bool TryParse(ReadOnlySpan<byte> line, in FixedWidthContext context, out TModel model)
        {
            if (line.Length < LineLength)
            {
                context.ReportShortLine(LineLength);
                model = default!;
                return false;
            }

            model = _factory!();
            foreach (ref readonly var processor in _parsers.AsSpan())
            {
                if (!processor.Parse(line.Slice(processor.Start, processor.Length), in context, ref model))
                {
                    context.ReportFailedColumn(processor.Name, processor.Start, processor.Length);
                    return false;
                }
            }
            return true;
        }

        internal void FormatLine(in TModel model, Span<byte> line, in FixedWidthContext context)
        {
            line.Fill((byte)' ');
            foreach (var formatter in _formatters!)
            {
                formatter.Format(in model, line, in context);
            }
        }

        internal void EnsureCanRead()
        {
            _readError?.Throw();
            if (_parsers is null || _factory is null)
            {
                throw new InvalidOperationException(
                    $"The layout for {typeof(TModel).Name} has a column without a setter; it cannot be used to read.");
            }
        }

        internal void EnsureCanWrite()
        {
            _writeError?.Throw();
            if (_formatters is null)
            {
                throw new InvalidOperationException(
                    $"The layout for {typeof(TModel).Name} has a column without a getter; it cannot be used to write.");
            }
        }
    }

    /// <summary>Creates <see cref="FixedWidthLayout{TModel}"/> instances.</summary>
    public static class FixedWidthLayout
    {
        /// <summary>
        /// Returns the layout declared by <typeparamref name="TModel"/>'s <c>[FixedColumn]</c> properties
        /// and fields, built once per type by reflection.
        /// </summary>
        /// <remarks>
        /// Not compatible with Native AOT, and trimming can remove the members it binds to. A
        /// <see langword="ref struct"/> model's layout reads but cannot write.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The layout is invalid: no columns, a negative start, a length below 1, overlapping columns, a non-ASCII padding or trim character, or a column type with no parser or formatter.</exception>
        [RequiresUnreferencedCode("Attribute layouts reflect over TModel's public properties and fields, which trimming may remove.")]
        [RequiresDynamicCode("Attribute layouts compile member accessors at runtime (Expression.Compile / MakeGenericType).")]
        public static FixedWidthLayout<TModel> FromAttributes<TModel>()
            where TModel : new(), allows ref struct
        {
            AttributeLayout<TModel>.BuildError?.Throw();
            return AttributeLayout<TModel>.Layout!;
        }

        private static class AttributeLayout<TModel>
            where TModel : new(), allows ref struct
        {
            internal static readonly FixedWidthLayout<TModel>? Layout;
            internal static readonly ExceptionDispatchInfo? BuildError;

#pragma warning disable CA1810, S3963 // The try/catch needs a static constructor.
            static AttributeLayout()
            {
                try
                {
                    Layout = Build();
                }
#pragma warning disable CA1031 // Rethrown from FromAttributes on every call.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    BuildError = ExceptionDispatchInfo.Capture(ex);
                }
            }
#pragma warning restore CA1810, S3963

            // A model may be readable and not writable (or the reverse): one side's error waits for that
            // side's first use. Only a model neither side can use fails here.
            private static FixedWidthLayout<TModel> Build()
            {
                var readError = TryBuildReader(out var parsers, out var factory);
                IColumnFormatter<TModel>[]? formatters = null;
                var writeError = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                    $"{typeof(TModel).Name} is a ref struct; an attribute layout can read it but not write it."));
                if (!typeof(TModel).IsByRefLike)
                {
                    writeError = TryBuildWriter(out formatters);
                }
                if (readError is not null && formatters is null)
                {
                    readError.Throw();
                }
                return new(parsers, factory, formatters, LineLength(), readError, writeError);
            }

            private static ExceptionDispatchInfo? TryBuildReader(out ColumnParserInfo<TModel>[]? parsers, out Func<TModel>? factory)
            {
                parsers = null;
                factory = null;
                try
                {
                    parsers = ParserBuilder.BuildProcessors<TModel>();
                    factory = ParserBuilder.BuildModelFactory<TModel>();
                    return null;
                }
#pragma warning disable CA1031 // Rethrown when the layout is first used to read.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    return ExceptionDispatchInfo.Capture(ex);
                }
            }

            private static ExceptionDispatchInfo? TryBuildWriter(out IColumnFormatter<TModel>[]? formatters)
            {
                formatters = null;
                try
                {
                    formatters = FormatterBuilder.Build<TModel>();
                    return null;
                }
#pragma warning disable CA1031 // Rethrown when the layout is first used to write.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    return ExceptionDispatchInfo.Capture(ex);
                }
            }

            private static int LineLength()
            {
                int lineLength = 0;
                ModelColumns.ForEachColumn(typeof(TModel), (_, attribute) => lineLength = Math.Max(lineLength, attribute.Start + attribute.Length));
                return lineLength;
            }
        }
    }
}
