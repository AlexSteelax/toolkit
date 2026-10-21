using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class MonotonicMaxBenchmarks
{
    private long _observed;

    /// <summary>
    ///     Single-reader cost of <see cref="MonotonicMax.Read" /> while <see cref="Threads" /> writers
    ///     continuously advance their slots. Writers perform <see cref="Count" /> updates in total,
    ///     so the reader races the writers for the whole invocation.
    /// </summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public void Read()
    {
        _value = Slots == 8 ? MonotonicMax.Create8() : MonotonicMax.Create16();
        var locals = new long[32 * Stride];

        var session = ContentionRunner.Start(Threads, Count, (id, _) =>
        {
            ref var local = ref locals[id * Stride];
            _value.Update(++local);
        });

        // Open the start barrier: writers and the reader enter the contended phase simultaneously.
        session.Release();

        for (var i = 0; i < Count; i++)
            _observed = _value.Read();

        session.Join();
        _ = _observed;
    }
}