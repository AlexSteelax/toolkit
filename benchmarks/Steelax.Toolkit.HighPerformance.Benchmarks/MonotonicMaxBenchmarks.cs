using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
///     Writer-side throughput of <see cref="MonotonicMax" /> under growing write contention.
///     Writers are hashed onto <see cref="Slots" /> cache-line-aligned slots; contention rises once
///     the number of writers exceeds the slot count. Each writer feeds its own cache-line-local
///     counter, so the only shared location contended is the slot under test.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public partial class MonotonicMaxBenchmarks
{
    public const int Count = 5_000_000;

    private const int Stride = 8;

    private MonotonicMax _value;

    [Params(8, 16)]
    public int Slots { get; set; }

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
    public void Update()
    {
        _value = Slots == 8 ? MonotonicMax.Create8() : MonotonicMax.Create16();
        var locals = new long[32 * Stride];

        // A per-writer counter (on its own cache line) grows monotonically, so every call reaches the
        // slot and attempts to raise it, with no shared contended counter.
        ContentionRunner.Run(Threads, Count, (id, _) =>
        {
            ref var local = ref locals[id * Stride];
            _value.Update(++local);
        });
    }
}