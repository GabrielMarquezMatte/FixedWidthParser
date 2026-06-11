using System.Collections.Concurrent;
using csFastFloat;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// UTF-8 counterpart of <see cref="ColumnParserRegistry"/>: a global, type-keyed registry of
    /// <see cref="Utf8ColumnValueParser{TValue}"/> delegates for the byte reader. To support a new
    /// column type, call <see cref="Register{TValue}"/> instead of writing a parser class. Types not
    /// registered here fall back to <see cref="IUtf8SpanParsable{TSelf}.TryParse"/>.
    /// <para>
    /// <c>double</c> and <c>float</c> are pre-registered with csFastFloat's UTF-8 overloads (which
    /// take the decimal separator as a <see langword="byte"/>). <c>string</c> is handled separately by
    /// the parser (UTF-8 decode, never fails) and is not part of this registry.
    /// </para>
    /// <para>
    /// Registration is process-wide and must happen before the first use of
    /// <see cref="Parsers.Utf8FixedWidthParser{TModel}"/> for a given model, since that type caches its
    /// column parsers in a static constructor. This registry is independent of the <c>char</c>
    /// <see cref="ColumnParserRegistry"/>; registering a type in one does not affect the other.
    /// </para>
    /// </summary>
    public static class Utf8ColumnParserRegistry
    {
        // Value: a Utf8ColumnValueParser<TValue> boxed as Delegate, keyed by typeof(TValue).
        private static readonly ConcurrentDictionary<Type, Delegate> _parsers = new();

        static Utf8ColumnParserRegistry()
        {
            Register(static (ReadOnlySpan<byte> span, IFormatProvider? formatProvider, out double value)
                => FastDoubleParser.TryParseDouble(span, out value, decimal_separator: (byte)CultureHelpers.GetDecimalSeparator(formatProvider)));
            Register(static (ReadOnlySpan<byte> span, IFormatProvider? formatProvider, out float value)
                => FastFloatParser.TryParseFloat(span, out value, decimal_separator: (byte)CultureHelpers.GetDecimalSeparator(formatProvider)));
        }

        /// <summary>Registers (or replaces) the UTF-8 value parser used for columns of type <typeparamref name="TValue"/>.</summary>
        public static void Register<TValue>(Utf8ColumnValueParser<TValue> parser)
        {
            _parsers[typeof(TValue)] = parser;
        }

        /// <summary>Removes a previously registered parser. Returns <see langword="false"/> if none was registered.</summary>
        public static bool Unregister<TValue>()
        {
            return _parsers.TryRemove(typeof(TValue), out _);
        }

        /// <summary>Looks up the registered parser for <paramref name="valueType"/>, or <see langword="null"/> if none.</summary>
        internal static Delegate? Get(Type valueType)
        {
            return _parsers.TryGetValue(valueType, out var parser) ? parser : null;
        }
    }
}
