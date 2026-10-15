using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Writers;

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

        /// <summary>Writes a model and returns the line without the trailing newline.</summary>
        public static string WriteOne<T>(FixedWidthWriter<T> writer, in T model)
        {
            using var ms = new MemoryStream();
            writer.Write(ms, in model);
            return Decode(ms).TrimEnd('\r', '\n');
        }

        public static async Task<string> WriteOneAsync<T>(FixedWidthWriter<T> writer, T model)
        {
            await using var ms = new MemoryStream();
            await writer.WriteAsync(ms, model, TestContext.Current.CancellationToken).ConfigureAwait(false);
            return Decode(ms).TrimEnd('\r', '\n');
        }

        public static string WriteMany<T>(FixedWidthWriter<T> writer, ReadOnlySpan<T> models)
        {
            using var ms = new MemoryStream();
            writer.WriteMany(ms, models);
            return Decode(ms);
        }

        public static string WriteMany<T>(FixedWidthWriter<T> writer, IEnumerable<T> models)
        {
            using var ms = new MemoryStream();
            writer.WriteMany(ms, models);
            return Decode(ms);
        }

        private static string Decode(MemoryStream ms)
        {
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
