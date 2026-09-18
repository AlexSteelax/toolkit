using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class ConduitChannelBenchmarks
{
    [Benchmark(OperationsPerInvoke = Count)]
    public async Task Conduit()
    {
        var queue = new Conduit<int>(Capacity, ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter);

        var producer = Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < Count; i++)
            {
                while (!queue.TryWrite(i))
                {
                    if (!await queue.WaitToWriteAsync())
                        break;
                }
            }

            queue.TryComplete();
        }, TaskCreationOptions.LongRunning).Unwrap();

        var consumer = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (queue.TryRead(out _))
                    continue;

                if (!await queue.WaitToReadAsync())
                    break;
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        await Task.WhenAll(producer, consumer);
    }
}