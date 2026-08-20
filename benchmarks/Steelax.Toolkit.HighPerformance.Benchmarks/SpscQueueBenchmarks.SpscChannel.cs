using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency;
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class SpscQueueBenchmarks
{
    [Benchmark(OperationsPerInvoke = Count)]
    public async Task SpscChannel()
    {
        var queue = new SpscChannel<int>(Capacity);

        var producer = Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < Count; i++)
            {
                while (!queue.TryWrite(i))
                    await queue.WaitToWriteAsync();
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