using System.Globalization;

namespace FixedWidthParser.Processors
{
    internal static class CultureHelpers
    {
        /// <summary>
        /// Decimal separator derived from the IFormatProvider (dot when null). Used by the
        /// double/float processors, which pass the character on to csFastFloat.
        /// </summary>
        public static char GetDecimalSeparator(IFormatProvider? formatProvider)
            => formatProvider is null
                ? '.'
                : NumberFormatInfo.GetInstance(formatProvider).NumberDecimalSeparator[0];
    }
}
