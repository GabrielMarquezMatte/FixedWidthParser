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
    /// <b>Lifecycle.</b> Registration is process-wide and affects every model. It must happen
    /// <i>before</i> the first use of <see cref="Parsers.Utf8FixedWidthParser{TModel}"/> for a given
    /// model: that type resolves and caches its column parsers in a static constructor (once per
    /// <c>TModel</c>), so a <see cref="Register{TValue}"/> or <see cref="Unregister{TValue}"/> call made
    /// after a model has first been parsed has <b>no effect</b> on that model's already-cached parsers.
    /// Register your custom types once at application startup.
    /// </para>
    /// <para>
    /// <b>Thread safety.</b> The backing store is a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>,
    /// so individual <see cref="Register{TValue}"/>/<see cref="Unregister{TValue}"/>/lookup calls are
    /// safe to call concurrently; there is no atomicity across the register-then-first-parse sequence.
    /// </para>
    /// <para>
    /// This registry is independent of the <c>char</c> <see cref="ColumnParserRegistry"/>; registering a
    /// type in one does not affect the other.
    /// </para>
    /// </summary>
    public static class Utf8ColumnParserRegistry
    {
        // Value parsers boxed as Delegate, keyed by typeof(TValue). See DelegateRegistry.
        private static readonly DelegateRegistry _store = new();

        static Utf8ColumnParserRegistry()
        {
            Register(static (ReadOnlySpan<byte> span, IFormatProvider? formatProvider, out double value)
                => FastDoubleParser.TryParseDouble(span, out value, decimal_separator: CultureHelpers.GetDecimalSeparatorByte(formatProvider)));
            Register(static (ReadOnlySpan<byte> span, IFormatProvider? formatProvider, out float value)
                => FastFloatParser.TryParseFloat(span, out value, decimal_separator: CultureHelpers.GetDecimalSeparatorByte(formatProvider)));
        }

        /// <summary>Registers (or replaces) the UTF-8 value parser used for columns of type <typeparamref name="TValue"/>.</summary>
        public static void Register<TValue>(Utf8ColumnValueParser<TValue> parser)
        {
            _store.Set(typeof(TValue), parser);
        }

        /// <summary>Removes a previously registered parser. Returns <see langword="false"/> if none was registered.</summary>
        public static bool Unregister<TValue>()
        {
            return _store.Remove(typeof(TValue));
        }

        /// <summary>Looks up the registered parser for <paramref name="valueType"/>, or <see langword="null"/> if none.</summary>
        internal static Delegate? Get(Type valueType)
        {
            return _store.Get(valueType);
        }
    }
}
