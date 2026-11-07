using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
///     Sustained CAS contention on a single shared field via <see cref="Interlocked.CompareExchange"/>.
///     Every call reads the shared field and proposes <c>current + 1</c>, so each operation is a real
///     CAS attempt on the contended cache line — a reference point for the CAS loops inside
///     <c>CompleteSignal</c>, <c>MonotonicMax</c> and friends.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Config))]
public class CompareExchangeBenchmarks
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
    public void CompareExchange()
    {
        _value = 0;

        // Each operation reads the shared field and tries to advance it by one, so every proposal
        // races the other writers on the same cache line regardless of the writer's local state.
        ContentionRunner.Run(Threads, Count, (id, _) =>
        {
            var current = Volatile.Read(ref _value);
            Interlocked.CompareExchange(ref _value, current + 1, current);
        });
    }
}