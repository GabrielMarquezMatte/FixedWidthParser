using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using FixedWidthParser.Infrastructure;
using static FixedWidthParser.Tests.TestHelpers;

namespace FixedWidthParser.Tests
{
    /// <summary>The writer's short-cut for plain doubles must match <see cref="double.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)"/> byte for byte.</summary>
    public class DoubleFormatTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("pt-BR")]
        [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
            Justification = "Needs a reproducible seeded PRNG for deterministic test content, not cryptographic randomness.")]
        public void FormattedBytesMatchDoubleTryFormat(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var context = CtxWith(culture);
            var options = new ColumnFormatOptions(Alignment.Right, ' ', null, OverflowBehavior.Throw);
            Random rng = new(42);
            List<double> values = [0, -0.0, 1e9, 999_999_999.9999, -999_999_999.9999, 0.0001, 0.00001, 0.00015, 0.1 + 0.2, 1.0 / 3, 1e15, 2.5, -2.5, double.Epsilon, double.MaxValue, double.NaN, double.NegativeInfinity];
            foreach (double edge in values.ToArray())
            {
                values.Add(Math.BitIncrement(edge));
                values.Add(Math.BitDecrement(edge));
            }
            for (int i = 0; i < 100_000; i++)
            {
                long digits = rng.NextInt64(-2_000_000_000_000, 2_000_000_000_000) / (long)Math.Pow(10, rng.Next(0, 12));
                values.Add(digits / Math.Pow(10, rng.Next(0, 7)));
                values.Add(rng.NextDouble() * Math.Pow(10, rng.Next(-8, 12)));
                values.Add(BitConverter.Int64BitsToDouble(rng.NextInt64()));
            }
            byte[] actual = new byte[32];
            foreach (double value in values)
            {
                FixedWidthRuntime.FormatUtf8Value(value, actual, in context, options, "Value");
                string text = value.ToString(culture);
                Assert.Equal(text.PadLeft(32 - Encoding.UTF8.GetByteCount(text) + text.Length), Encoding.UTF8.GetString(actual));
            }
        }
    }
}
