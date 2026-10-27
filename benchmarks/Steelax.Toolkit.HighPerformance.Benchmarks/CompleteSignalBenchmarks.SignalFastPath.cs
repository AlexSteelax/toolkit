using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class CompleteSignalBenchmarks
{
    private bool _fast;

    /// <summary>
    ///     Single-threaded round-trip on the synchronous fast path: a readiness event is charged by
    ///     <see cref="CompleteSignal.Signal"/> and consumed by the immediately-following
    ///     <see cref="CompleteSignal.WaitAsync"/> without ever registering a waiter, so the handshake
    ///     is two CAS loops in <c>_handshake</c> and no <see cref="ValueTask{TResult}"/> allocation.
    /// </summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public void SignalFastPath()
    {
        var signal = new CompleteSignal(AllowSynchronousContinuations);

        for (var i = 0; i < Count; i++)
        {
            signal.Signal();

            _fast = signal.WaitAsync().IsCompletedSuccessfully;
        }
    }
}