using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Fast allocation-regression guard (runs in milliseconds, unlike the BenchmarkDotNet suite).
    /// Proves the pooled read hot path allocates nothing per line: with a <see cref="StringPool"/>
    /// and a repeated value, the only would-be per-line allocation (the string column) is interned
    /// away, and the struct enumerator adds no heap per iteration. We read two different line counts
    /// and assert the <em>marginal</em> per-line cost is ~0 — subtracting the two cancels the fixed
    /// per-call overhead (enumerable, closure, buffer rent) and JIT/warmup noise, so the test is
    /// robust without pinning exact byte counts.
    /// </summary>
    public class AllocationGuardTests
    {
        /// <summary>
        /// <see cref="FixedWidthByteReader{TModel}.Read(Stream, bool)"/> stores the stream
        /// directly instead of a <c>Func&lt;Stream&gt;</c>, so a call allocates only the enumerable
        /// object — no captured-variable display class and no delegate. Measured at 64 B/call; a
        /// regressed closure would add a delegate (~56 B+) and push it well past the threshold.
        /// </summary>
        [Fact]
        public void ByteReaderRead_AllocatesEnumerableOnly_NoClosure()
        {
            var reader = new FixedWidthByteReader<CodeModel>();
            using var stream = new MemoryStream();
            _ = reader.Read(stream); // warmup/JIT

            const int n = 10_000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < n; i++)
            {
                _ = reader.Read(stream);
            }

            double perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)n;

            Assert.True(perCall <= 80, $"Read allocated {perCall} B/call (expected ~64 B: enumerable only, no closure).");
        }

        /// <summary>
        /// <see cref="FixedWidthReader{TModel}.Read(TextReader)"/> stores the source in a
        /// by-value <c>TextReaderSource</c> struct instead of a <c>Func&lt;TextReader&gt;</c>, so a call
        /// allocates only the enumerable object — no captured-variable display class and no delegate.
        /// Measured at 88 B/call; a regressed closure would add a delegate (~56 B+) past the threshold.
        /// </summary>
        [Fact]
        public void CharReaderRead_AllocatesEnumerableOnly_NoClosure()
        {
            var reader = new FixedWidthReader<CodeModel>();
            using var tr = new StringReader("");
            _ = reader.Read(tr); // warmup/JIT

            const int n = 10_000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < n; i++)
            {
                _ = reader.Read(tr);
            }

            double perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)n;

            Assert.True(perCall <= 104, $"Read allocated {perCall} B/call (expected ~88 B: enumerable only, no closure).");
        }

        /// <summary>
        /// <see cref="FixedWidthUtf8.Read{TModel}(Stream, bool, IFormatProvider?, StringPool?, int)"/>
        /// stores the stream directly (like the reflection byte reader), so a call allocates only the
        /// enumerable object — no captured-variable display class and no delegate.
        /// </summary>
        [Fact]
        public void GeneratedUtf8Read_AllocatesEnumerableOnly_NoClosure()
        {
            using var stream = new MemoryStream();
            _ = FixedWidthUtf8.Read<GenCodeModel>(stream); // warmup/JIT

            const int n = 10_000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < n; i++)
            {
                _ = FixedWidthUtf8.Read<GenCodeModel>(stream);
            }

            double perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)n;

            Assert.True(perCall <= 80, $"Read allocated {perCall} B/call (expected ~64 B: enumerable only, no closure).");
        }

        private const string Line = "ABC";   // CodeModel/GenCodeModel: [FixedColumn(0, 3)] Code
        private const int Small = 2_000;
        private const int Large = 6_000;

        private static string Repeat(int lines)
        {
            var sb = new StringBuilder((Line.Length + 1) * lines);
            for (int i = 0; i < lines; i++)
            {
                sb.Append(Line).Append('\n');
            }

            return sb.ToString();
        }

        [Fact]
        public void ReflectionPooledReader_AllocatesNothingPerLine()
        {
            var reader = new FixedWidthReader<CodeModel>(CultureInfo.InvariantCulture, new StringPool());
            string small = Repeat(Small), large = Repeat(Large);

            // Warmup: JIT every path and intern "ABC" into the pool.
            SumReflection(reader, small);
            SumReflection(reader, large);

            double perLine = MarginalPerLine(() => SumReflection(reader, small), () => SumReflection(reader, large));
            Assert.True(perLine <= 1, $"Reflection pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        [Fact]
        public void GeneratedPooledReader_AllocatesNothingPerLine()
        {
            var pool = new StringPool();
            string small = Repeat(Small), large = Repeat(Large);

            SumGenerated(pool, small);
            SumGenerated(pool, large);

            double perLine = MarginalPerLine(() => SumGenerated(pool, small), () => SumGenerated(pool, large));
            Assert.True(perLine <= 1, $"Generated pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        [Fact]
        public void GeneratedUtf8PooledReader_AllocatesNothingPerLine()
        {
            var pool = new StringPool();
            byte[] small = Encoding.UTF8.GetBytes(Repeat(Small)), large = Encoding.UTF8.GetBytes(Repeat(Large));

            SumGeneratedUtf8(pool, small);
            SumGeneratedUtf8(pool, large);

            double perLine = MarginalPerLine(() => SumGeneratedUtf8(pool, small), () => SumGeneratedUtf8(pool, large));
            Assert.True(perLine <= 1, $"Generated UTF-8 pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        private static int SumReflection(FixedWidthReader<CodeModel> reader, string text)
        {
            int total = 0;
            using var sr = new StringReader(text);
            foreach (var m in reader.Read(sr))
            {
                total += m.Code.Length;
            }

            return total;
        }

        private static int SumGenerated(StringPool pool, string text)
        {
            int total = 0;
            using var sr = new StringReader(text);
            foreach (var m in FixedWidth.Read<GenCodeModel>(sr, CultureInfo.InvariantCulture, pool))
            {
                total += m.Code.Length;
            }

            return total;
        }

        private static int SumGeneratedUtf8(StringPool pool, byte[] bytes)
        {
            int total = 0;
            using var ms = new MemoryStream(bytes, writable: false);
            foreach (var m in FixedWidthUtf8.Read<GenCodeModel>(ms, formatProvider: CultureInfo.InvariantCulture, stringPool: pool))
            {
                total += m.Code.Length;
            }

            return total;
        }

        // Bytes allocated per extra line, isolating per-line cost from fixed per-call overhead.
        private static double MarginalPerLine(Action readSmall, Action readLarge)
        {
            long b1 = GC.GetAllocatedBytesForCurrentThread();
            readSmall();
            long forSmall = GC.GetAllocatedBytesForCurrentThread() - b1;

            long b2 = GC.GetAllocatedBytesForCurrentThread();
            readLarge();
            long forLarge = GC.GetAllocatedBytesForCurrentThread() - b2;

            return (double)(forLarge - forSmall) / (Large - Small);
        }
    }
}
