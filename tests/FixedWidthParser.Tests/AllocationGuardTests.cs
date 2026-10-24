using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FixedWidthParser.Tests
{
    /// <summary>
    /// Fast allocation-regression guard (runs in milliseconds, unlike the BenchmarkDotNet suite).
    /// Proves the pooled read hot path allocates nothing per line: with a <see cref="StringPool"/> and a
    /// repeated value, the string column is interned away and the struct enumerator adds no heap per
    /// iteration. Reading two line counts and subtracting cancels the fixed per-call overhead.
    /// </summary>
    public class AllocationGuardTests
    {
        private const string Line = "ABC"; // CodeModel/GenCodeModel: [FixedColumn(0, 3)] Code
        private const int Small = 2_000;
        private const int Large = 6_000;

        /// <summary>
        /// A read call stores the stream directly instead of a <c>Func&lt;Stream&gt;</c>, so it allocates
        /// only the enumerable — no display class and no delegate (~56 B more if one sneaks back in).
        /// </summary>
        [Fact]
        public void Read_AllocatesEnumerableOnly_NoClosure()
        {
            var layout = FixedWidthLayout.FromAttributes<CodeModel>();
            using var stream = new MemoryStream();
            _ = FixedWidth.Read(stream, layout);
            _ = FixedWidth.Read<GenCodeModel>(stream);

            Assert.True(PerCall(() => FixedWidth.Read(stream, layout)) <= 72, "Reflection Read allocated more than the enumerable.");
            Assert.True(PerCall(() => FixedWidth.Read<GenCodeModel>(stream)) <= 72, "Generated Read allocated more than the enumerable.");
        }

        [Fact]
        public void ReflectionPooledReader_AllocatesNothingPerLine()
        {
            var options = new FixedWidthReaderOptions { StringPool = new StringPool() };
            var layout = FixedWidthLayout.FromAttributes<CodeModel>();
            byte[] small = Repeat(Small), large = Repeat(Large);
            SumReflection(layout, options, small);
            SumReflection(layout, options, large);

            double perLine = MarginalPerLine(() => SumReflection(layout, options, small), () => SumReflection(layout, options, large));

            Assert.True(perLine <= 1, $"Reflection pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        [Fact]
        public void GeneratedPooledReader_AllocatesNothingPerLine()
        {
            var options = new FixedWidthReaderOptions { StringPool = new StringPool() };
            byte[] small = Repeat(Small), large = Repeat(Large);
            SumGenerated(options, small);
            SumGenerated(options, large);

            double perLine = MarginalPerLine(() => SumGenerated(options, small), () => SumGenerated(options, large));

            Assert.True(perLine <= 1, $"Generated pooled reader regressed to {perLine} B/line (expected ~0).");
        }

        private static double PerCall(Func<object> read)
        {
            const int n = 10_000;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < n; i++)
            {
                _ = read();
            }
            return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)n;
        }

        private static byte[] Repeat(int lines)
        {
            var sb = new StringBuilder((Line.Length + 1) * lines);
            for (int i = 0; i < lines; i++)
            {
                sb.Append(Line).Append('\n');
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static int SumReflection(FixedWidthLayout<CodeModel> layout, FixedWidthReaderOptions options, byte[] bytes)
        {
            int total = 0;
            foreach (var m in FixedWidth.Read(new MemoryStream(bytes, writable: false), layout, options: options))
            {
                total += m.Code.Length;
            }
            return total;
        }

        private static int SumGenerated(FixedWidthReaderOptions options, byte[] bytes)
        {
            int total = 0;
            foreach (var m in FixedWidth.Read<GenCodeModel>(new MemoryStream(bytes, writable: false), options: options))
            {
                total += m.Code.Length;
            }
            return total;
        }

        // Bytes allocated per extra line, isolating per-line cost from fixed per-call overhead. The best of
        // three attempts: while other test classes run, the runtime can make a one-off allocation on this
        // thread during one read, which a per-line regression would not survive in every attempt.
        private static double MarginalPerLine(Action readSmall, Action readLarge)
        {
            double best = double.MaxValue;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                best = Math.Min(best, MarginalPerLineOnce(readSmall, readLarge));
            }
            return best;
        }

        private static double MarginalPerLineOnce(Action readSmall, Action readLarge)
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
