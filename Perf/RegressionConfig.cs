using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Order;

namespace Benchmarks.Perf
{
    /// <summary>
    /// Configuração base para detecção de regressão: ordena por velocidade, mede alocações
    /// e exporta JSON completo em BenchmarkDotNet.Artifacts/results para comparar entre commits.
    /// </summary>
    public sealed class RegressionConfig : ManualConfig
    {
        public RegressionConfig()
        {
            // MemoryDiagnoser: aloca por op (chave para regressões de GC).
            AddDiagnoser(MemoryDiagnoser.Default);
            // JSON completo em BenchmarkDotNet.Artifacts/results para arquivar/comparar entre commits.
            // (O MarkdownExporter.GitHub já vem no config padrão do BenchmarkSwitcher.)
            AddExporter(JsonExporter.Full);
            WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        }
    }
}
