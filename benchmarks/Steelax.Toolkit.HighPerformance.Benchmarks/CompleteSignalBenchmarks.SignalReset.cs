using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class CompleteSignalBenchmarks
{
    private bool _reset;

    /// <summary>
    ///     Signal/reset churn without a waiter: <see cref="CompleteSignal.Signal"/> charges the
    ///     readiness bit and <see cref="CompleteSignal.TryReset"/> drains it, so the event is never
    ///     awaited. This is the cheapest possible use — a locally-polled edge — and isolates the cost
    ///     of the two CAS loops from any value-task machinery.
    /// </summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public void SignalReset()
    {
        var signal = new CompleteSignal(AllowSynchronousContinuations);

        for (var i = 0; i < Count; i++)
        {
            signal.Signal();

            _reset = signal.TryReset();
        }
    }
}