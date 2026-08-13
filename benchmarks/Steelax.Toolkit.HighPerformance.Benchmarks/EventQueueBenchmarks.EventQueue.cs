using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class EventQueueBenchmarks
{
    // [Benchmark(OperationsPerInvoke = 1_000)]
    // public Task EventQueue_Async_1k() => EventQueue(1_000, Capacity, false);
    
    // [Benchmark(OperationsPerInvoke = 1_000_000)]
    // public Task EventQueue_Sync_1kk() => EventQueue(1_000_000, Capacity, true);

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public async Task EventQueue_Waiting_1kk()
    {
        const int count = 1_000_000;
        
        var queue = new EventQueue<int>(Capacity, true);
        var fan = new FanInSlim();

        queue.OnWriteReady += fan.GetSignalCallback(0).Handler;
        
        var producer = Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < count; i++)
            {
                while (!queue.TryWrite(i))
                {
                    await fan.WaitAsync();
                    _ = fan.Take();
                }
            }

            queue.Complete();
        }, TaskCreationOptions.LongRunning).Unwrap();

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