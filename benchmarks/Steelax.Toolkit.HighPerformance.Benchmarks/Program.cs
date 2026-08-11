using BenchmarkDotNet.Running;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
