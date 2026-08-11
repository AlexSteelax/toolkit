using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class EventQueueBenchmarks
{
    [Benchmark(OperationsPerInvoke = 1_000)]
    public Task EventQueue_Async_1k() => EventQueue(1_000, Capacity, false);
    
    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public Task EventQueue_Sync_1kk() => EventQueue(1_000_000, Capacity, true);
    
    private static async Task EventQueue(int count, int capacity, bool allowSynchronousContinuations)
    {
        var queue = new EventQueue<int>(capacity, allowSynchronousContinuations);
        
        var producer = Task.Factory.StartNew(() =>
        {
            var spin = new SpinWait();

            for (var i = 0; i < count; i++)
            {
                while (!queue.TryWrite(i))
                    spin.SpinOnce();
            }

            queue.Complete();
        }, TaskCreationOptions.LongRunning);


        var consumer = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (queue.TryRead(out _, out var completed))
                    continue;

                if (completed)
                    break;
                
                await queue.WaitToReadAsync();
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        await Task.WhenAll(producer, consumer);
    }
}