using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Order;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Base config for regression detection: orders by speed, measures allocations and exports
    /// full JSON to BenchmarkDotNet.Artifacts/results for comparison across commits.
    /// </summary>
    public sealed class RegressionConfig : ManualConfig
    {
        public RegressionConfig()
        {
            // MemoryDiagnoser: allocations per op (key for GC regressions).
            AddDiagnoser(MemoryDiagnoser.Default);
            // Full JSON in BenchmarkDotNet.Artifacts/results to archive/compare across commits.
            // (MarkdownExporter.GitHub already ships in the BenchmarkSwitcher default config.)
            AddExporter(JsonExporter.Full);
            WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        }
    }
}
