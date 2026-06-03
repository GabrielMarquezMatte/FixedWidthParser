using System.Globalization;
using System.Text;
using FixedWidthParser.Writers;

namespace FixedWidthParser.Tests
{
    internal static class TestHelpers
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Writes a model and returns the line without the trailing newline.</summary>
        public static string WriteOne<T>(FixedWidthWriter<T> writer, in T model, IFormatProvider? formatProvider = null)
        {
            using var ms = new MemoryStream();
            writer.Write(ms, in model, formatProvider ?? Inv);
            return Decode(ms).TrimEnd('\r', '\n');
        }

        public static async Task<string> WriteOneAsync<T>(FixedWidthWriter<T> writer, T model)
        {
            using var ms = new MemoryStream();
            await writer.WriteAsync(ms, model, Inv);
            return Decode(ms).TrimEnd('\r', '\n');
        }

        public static string WriteMany<T>(FixedWidthWriter<T> writer, ReadOnlySpan<T> models)
        {
            using var ms = new MemoryStream();
            writer.WriteMany(ms, models, Inv);
            return Decode(ms);
        }

        public static string WriteMany<T>(FixedWidthWriter<T> writer, IEnumerable<T> models)
        {
            using var ms = new MemoryStream();
            writer.WriteMany(ms, models, Inv);
            return Decode(ms);
        }

        private static string Decode(MemoryStream ms) => Encoding.UTF8.GetString(ms.ToArray());
    }
}
