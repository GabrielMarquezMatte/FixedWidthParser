using System.Runtime.CompilerServices;
using FixedWidthParser.Infrastructure;

namespace FixedWidthParser.Readers
{
    /// <summary>What a parser reported about the line it rejected, filled during a reader's diagnosis re-parse.</summary>
    internal sealed class ParseFailure
    {
        internal string? ColumnName { get; private set; }
        internal int Start { get; private set; } = -1;
        internal int Length { get; private set; }
        internal int RequiredLength { get; private set; }

        internal void SetColumn(string columnName, int start, int length)
        {
            ColumnName = columnName;
            Start = start;
            Length = length;
        }

        internal void SetShortLine(int requiredLength)
        {
            RequiredLength = requiredLength;
        }
    }

    /// <summary>
    /// The slow paths shared by every reader. A line that fails to parse is parsed again with a failure
    /// sink, so the hot path pays nothing to learn which column rejected it.
    /// </summary>
    internal static class ParseErrors
    {
        /// <summary>
        /// Whether an enumerator should yield <paramref name="line"/>. Without
        /// <see cref="FixedWidthReaderOptions.OnParseError"/> every line is yielded and a bad one throws when
        /// <see cref="Parse"/> reads it; with it, the line is parsed here so a bad one can be skipped.
        /// </summary>
        internal static bool Accepts<TModel, TParser>(
            TParser parser, ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context, Func<FixedWidthParseException, bool>? onParseError)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
        {
            if (onParseError is null || parser.TryParse(line, in context, out _))
            {
                return true;
            }
            HandleFailure<TModel, TParser>(parser, line, lineNumber, in context, onParseError);
            return false;
        }

        /// <summary>Parses the line an enumerator sits on, throwing as a reader does when it does not parse.</summary>
        internal static TModel Parse<TModel, TParser>(TParser parser, ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
        {
            if (!parser.TryParse(line, in context, out var model))
            {
                HandleFailure<TModel, TParser>(parser, line, lineNumber, in context, onParseError: null);
            }
            return model;
        }

        /// <summary>Returns when <see cref="FixedWidthReaderOptions.OnParseError"/> asks to skip the line; throws otherwise.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void HandleFailure<TModel, TParser>(
            TParser parser, ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context, Func<FixedWidthParseException, bool>? onParseError)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
        {
            Reject(Diagnose<TModel, TParser>(parser, line, lineNumber, in context), onParseError);
        }

        /// <summary>Returns when <paramref name="onParseError"/> asks to skip the line <paramref name="exception"/> describes; throws it otherwise.</summary>
        internal static void Reject(FixedWidthParseException exception, Func<FixedWidthParseException, bool>? onParseError)
        {
            if (onParseError is null || !onParseError(exception))
            {
                throw exception;
            }
        }

        /// <summary>Parses a rejected line again with a failure sink and builds the exception that names its column.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static FixedWidthParseException Diagnose<TModel, TParser>(TParser parser, ReadOnlySpan<byte> line, long lineNumber, in FixedWidthContext context)
            where TModel : allows ref struct
            where TParser : struct, IRecordLineParser<TModel>
        {
            var failure = new ParseFailure();
            var diagnosis = context with { Failure = failure };
            _ = parser.TryParse(line, in diagnosis, out _);
            return Create(line, lineNumber, typeof(TModel).Name, failure, in context);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidDataException LineTooLong(long lineNumber, int maxLineLength)
        {
            return new InvalidDataException(
                $"Line {lineNumber} is longer than {maxLineLength} bytes (FixedWidthReaderOptions.MaxLineLength).");
        }

        private static FixedWidthParseException Create(ReadOnlySpan<byte> line, long lineNumber, string modelName, ParseFailure failure, in FixedWidthContext context)
        {
            if (failure.RequiredLength > line.Length)
            {
                return new FixedWidthParseException(
                    $"Line {lineNumber} could not be parsed into {modelName}: it is {line.Length} bytes long and the layout needs {failure.RequiredLength}.",
                    lineNumber, columnName: null, columnStart: -1, columnLength: 0, rawValue: null);
            }

            if (failure.ColumnName is { } columnName && failure.Start >= 0 && failure.Start + failure.Length <= line.Length)
            {
                string rawValue = context.Encoding.GetString(line.Slice(failure.Start, failure.Length));
                return new FixedWidthParseException(
                    $"Line {lineNumber} could not be parsed into {modelName}: column '{columnName}' (start {failure.Start}, length {failure.Length}) holds \"{rawValue}\".",
                    lineNumber, columnName, failure.Start, failure.Length, rawValue);
            }

            return new FixedWidthParseException(
                $"Line {lineNumber} could not be parsed into {modelName}: \"{context.Encoding.GetString(line)}\".",
                lineNumber, columnName: null, columnStart: -1, columnLength: 0, rawValue: null);
        }
    }
}
