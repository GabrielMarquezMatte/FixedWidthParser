using csFastFloat;

namespace FixedWidthParser.Processors
{
    /// <summary>
    /// Global, type-keyed registry of <see cref="ColumnValueParser{TValue}"/> delegates. To support
    /// a new column type, call <see cref="Register{TValue}"/> instead of writing a processor class.
    /// Types not registered here fall back to <see cref="ISpanParsable{TSelf}.TryParse"/>.
    /// <para>
    /// <c>double</c> and <c>float</c> are pre-registered with csFastFloat. <c>string</c> is handled
    /// separately by the parser (string pooling, never fails) and is not part of this registry.
    /// </para>
    /// <para>
    /// Registration is process-wide and must happen before the first use of
    /// <see cref="Parsers.FixedWidthParser{TModel}"/> for a given model, since that type caches its
    /// column parsers in a static constructor.
    /// </para>
    /// </summary>
    public static class ColumnParserRegistry
    {
        // Value parsers boxed as Delegate, keyed by typeof(TValue). See DelegateRegistry.
        private static readonly DelegateRegistry _store = new();

        static ColumnParserRegistry()
        {
            Register(static (ReadOnlySpan<char> span, IFormatProvider? formatProvider, out double value)
                => FastDoubleParser.TryParseDouble(span, out value, decimal_separator: CultureHelpers.GetDecimalSeparator(formatProvider)));
            Register(static (ReadOnlySpan<char> span, IFormatProvider? formatProvider, out float value)
                => FastFloatParser.TryParseFloat(span, out value, decimal_separator: CultureHelpers.GetDecimalSeparator(formatProvider)));
        }

        /// <summary>Registers (or replaces) the value parser used for columns of type <typeparamref name="TValue"/>.</summary>
        public static void Register<TValue>(ColumnValueParser<TValue> parser)
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
