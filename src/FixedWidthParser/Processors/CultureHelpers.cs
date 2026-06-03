using System.Globalization;

namespace FixedWidthParser.Processors
{
    internal static class CultureHelpers
    {
        /// <summary>
        /// Separador decimal derivado do IFormatProvider (ponto quando nulo). Usado pelos
        /// processadores de double/float, que repassam o caractere ao csFastFloat.
        /// </summary>
        public static char GetDecimalSeparator(IFormatProvider? formatProvider)
            => formatProvider is null
                ? '.'
                : NumberFormatInfo.GetInstance(formatProvider).NumberDecimalSeparator[0];
    }
}
