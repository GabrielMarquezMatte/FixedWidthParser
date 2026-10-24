using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Tests
{
    internal static class TestHelpers
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Invariant culture, UTF-8, no pool.</summary>
        public static readonly FixedWidthContext Ctx = default;

        public static FixedWidthContext CtxWith(IFormatProvider? provider = null, StringPool? pool = null, Encoding? encoding = null)
        {
            return new FixedWidthContext { FormatProvider = provider!, StringPool = pool, Encoding = encoding! };
        }

        public static byte[] Bytes(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        public static MemoryStream StreamOf(string content)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>Formats a generated model and returns the line as UTF-8 text.</summary>
        public static string Format<T>(in T model, in FixedWidthContext context = default) where T : IFixedWidthModel<T>
        {
            Span<byte> destination = stackalloc byte[4096];
            Assert.True(FixedWidth.TryFormat(in model, destination, in context, out int written));
            return Encoding.UTF8.GetString(destination[..written]);
        }

        /// <summary>Writes a model through an attribute or fluent layout and returns the line without the trailing newline.</summary>
        public static string WriteOne<T>(FixedWidthLayout<T> layout, in T model, FixedWidthWriterOptions? options = null)
        {
            using var ms = new MemoryStream();
            FixedWidth.Write(ms, in model, layout, options);
            return Decode(ms).TrimEnd('\r', '\n');
        }

        public static async Task<string> WriteOneAsync<T>(FixedWidthLayout<T> layout, T model, FixedWidthWriterOptions? options = null)
        {
            await using var ms = new MemoryStream();
            await FixedWidth.WriteAsync(ms, model, layout, options, TestContext.Current.CancellationToken).ConfigureAwait(false);
            return Decode(ms).TrimEnd('\r', '\n');
        }

        public static string WriteMany<T>(FixedWidthLayout<T> layout, ReadOnlySpan<T> models, FixedWidthWriterOptions? options = null)
        {
            using var ms = new MemoryStream();
            FixedWidth.WriteMany(ms, models, layout, options);
            return Decode(ms);
        }

        public static string WriteMany<T>(FixedWidthLayout<T> layout, IEnumerable<T> models, FixedWidthWriterOptions? options = null)
        {
            using var ms = new MemoryStream();
            FixedWidth.WriteMany(ms, models, layout, options);
            return Decode(ms);
        }

        private static string Decode(MemoryStream ms)
        {
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
