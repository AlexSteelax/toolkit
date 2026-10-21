using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
///     Sustained CAS contention on a single shared field via <see cref="AdvancedInterlocked.CompareExchangeMax" />.
///     Each writer keeps its own cache-line-local counter (no secondary contention) and proposes
///     max(local, current) + 1, so every call performs a real CAS attempt instead of the no-op fast path.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class CompareExchangeMaxBenchmarks
{
    public const int Count = 5_000_000;

    private const int Stride = 8;

    private long _value;

    [Params(1, 2, 4, 8, 16, 32)]
    public int Threads { get; set; }

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams);
            AddJob(Job.Default
                .WithWarmupCount(5)
                .WithMinIterationCount(10)
                .WithMaxIterationCount(50));
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void CompareExchangeMax()
    {
        _value = 0;
        var locals = new long[32 * Stride];

        // A per-writer counter lives on its own cache line, while pushing through current
        // guarantees the next value always exceeds the observed maximum.
        ContentionRunner.Run(Threads, Count, (id, _) =>
        {
            ref var local = ref locals[id * Stride];
            var next = Math.Max(local, Volatile.Read(ref _value)) + 1;

            AdvancedInterlocked.CompareExchangeMax(ref _value, next);
            local = next;
        });
    }
}