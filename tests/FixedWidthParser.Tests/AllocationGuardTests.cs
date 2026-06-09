using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FixedWidthParser.Readers;
using Xunit;

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
        private const string Line = "ABC";   // CodeModel/GenCodeModel: [FixedColumn(0, 3)] Code
        private const int Small = 2_000;
        private const int Large = 6_000;

        private static string Repeat(int lines)
        {
            var sb = new StringBuilder((Line.Length + 1) * lines);
            for (int i = 0; i < lines; i++) sb.Append(Line).Append('\n');
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

            long perLine = MarginalPerLine(() => SumReflection(reader, small), () => SumReflection(reader, large));
            Assert.True(perLine <= 1, $"Reflection pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        [Fact]
        public void GeneratedPooledReader_AllocatesNothingPerLine()
        {
            var pool = new StringPool();
            string small = Repeat(Small), large = Repeat(Large);

            SumGenerated(pool, small);
            SumGenerated(pool, large);

            long perLine = MarginalPerLine(() => SumGenerated(pool, small), () => SumGenerated(pool, large));
            Assert.True(perLine <= 1, $"Generated pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        private static int SumReflection(FixedWidthReader<CodeModel> reader, string text)
        {
            int total = 0;
            using var sr = new StringReader(text);
            foreach (var m in reader.Read(sr)) total += m.Code.Length;
            return total;
        }

        private static int SumGenerated(StringPool pool, string text)
        {
            int total = 0;
            using var sr = new StringReader(text);
            foreach (var m in FixedWidth.Read<GenCodeModel>(sr, CultureInfo.InvariantCulture, pool)) total += m.Code.Length;
            return total;
        }

        // Bytes allocated per extra line, isolating per-line cost from fixed per-call overhead.
        private static long MarginalPerLine(Action readSmall, Action readLarge)
        {
            long b1 = GC.GetAllocatedBytesForCurrentThread();
            readSmall();
            long forSmall = GC.GetAllocatedBytesForCurrentThread() - b1;

            long b2 = GC.GetAllocatedBytesForCurrentThread();
            readLarge();
            long forLarge = GC.GetAllocatedBytesForCurrentThread() - b2;

            return (forLarge - forSmall) / (Large - Small);
        }
    }
}
