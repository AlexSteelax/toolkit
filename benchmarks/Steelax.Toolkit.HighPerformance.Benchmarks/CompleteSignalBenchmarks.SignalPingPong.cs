using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class CompleteSignalBenchmarks
{
    /// <summary>
    ///     Strict SPSC ping-pong through registered waiters, mirroring the
    ///     <c>PingPong_NoTimeout</c> test: the writer signals <c>csReader</c> and awaits the
    ///     acknowledgement on <c>csWriter</c>, while the reader re-registers via
    ///     <see cref="CompleteSignal.WaitAsync"/> and acknowledges every wake. Because the writer never
    ///     signals until the reader is parked, every iteration pays the waiter registration CAS, the
    ///     <c>SetResult</c> wake and the continuation scheduling — the full value-task handshake, never
    ///     the charged <c>Ready</c> fast path.
    /// </summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public async Task SignalPingPong()
    {
        var csReader = new CompleteSignal(AllowSynchronousContinuations);
        var csWriter = new CompleteSignal(AllowSynchronousContinuations);

        var reader = Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < Count; i++)
            {
                if (!await csReader.WaitAsync())
                    break;

                csWriter.Signal();
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        for (var i = 0; i < Count; i++)
        {
            csReader.Signal();

            if (!await csWriter.WaitAsync())
                break;
        }

        // Break the chain so the reader terminates even if the handshake ended asymmetrically.
        csWriter.Complete();
        csReader.Complete();

        await reader;
    }
}