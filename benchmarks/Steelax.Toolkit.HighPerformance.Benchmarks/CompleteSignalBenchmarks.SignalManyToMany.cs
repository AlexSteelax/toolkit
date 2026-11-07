using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class CompleteSignalBenchmarks
{
    /// <summary>
    ///     Contended signaling: <see cref="Threads"/> writers each fire <c>Count / Threads</c> signals
    ///     into a single <see cref="CompleteSignal"/> while one reader re-registers via
    ///     <see cref="CompleteSignal.WaitAsync"/> after every wake. As in the
    ///     <c>ManySignallers_SingleWaiter</c> test, under contention most signals land on an
    ///     already-charged <c>Ready</c> bit and <see cref="CompleteSignal.Signal"/> returns early, so
    ///     the observed wake count stays below the total; the handshake is broken with
    ///     <see cref="CompleteSignal.Complete"/> so the reader terminates.
    /// </summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public async Task SignalManyToMany()
    {
        var signal = new CompleteSignal(AllowSynchronousContinuations);

        var reader = Task.Factory.StartNew(async () =>
        {
            var observed = 0;

            while (await signal.WaitAsync())
                observed++;

            return observed;
        }, TaskCreationOptions.LongRunning).Unwrap();

        var writers = new Task[Threads];

        for (var t = 0; t < Threads; t++)
        {
            writers[t] = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < Count / Threads; i++)
                    signal.Signal();
            });
        }

        await Task.WhenAll(writers);

        // Break the handshake: the reader is parked in WaitAsync and must observe the latch.
        signal.Complete();

        await reader;
    }
}