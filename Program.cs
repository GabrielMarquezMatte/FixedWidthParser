using BenchmarkDotNet.Running;
using Benchmarks.Attributes;
using Benchmarks.Parsers;

namespace Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
