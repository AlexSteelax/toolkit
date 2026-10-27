using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
///     Round-trip cost of <see cref="CompleteSignal"/>: the synchronous fast path (a readiness event
///     already charged), the signal/reset churn without a waiter, the full SPSC handshake through
///     <see cref="CompleteSignal.WaitAsync"/> and a contended producer set fighting over a single
///     shared readiness flag.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Config(typeof(Config))]
public partial class CompleteSignalBenchmarks
{
    /// <summary>Number of signal handshakes per invocation.</summary>
    public const int Count = 1_000_000;

    /// <summary>
    ///     Whether continuations of registered waiters may run synchronously on the signalling thread
    ///     (the <see cref="CompleteSignal"/> constructor flag).
    /// </summary>
    [Params(true, false)]
    public bool AllowSynchronousContinuations { get; set; }

    /// <summary>Number of concurrent writers for the contended scenario.</summary>
    [Params(1, 2, 4, 8, 16)]
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
}