using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class SpscQueueBenchmarks
{
    [Benchmark(OperationsPerInvoke = 1_000)]
    public Task SpscChannel_Async_1k() => SpscChannel(1_000, Capacity, allowSynchronousContinuations: false);
    
    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public Task SpscChannel_Sync_1kk() => SpscChannel(1_000_000, Capacity, allowSynchronousContinuations: true);

    [Benchmark(OperationsPerInvoke = 10_000_000)]
    public async Task SpscChannel_Waiting_10kk()
    {
        const int count = 10_000_000;
        
        var queue = new SpscChannel<int>(Capacity, allowSynchronousContinuations: true);

        var producer = Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < count; i++)
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

                if (queue.IsCompleted)
                    break;
                
                await queue.WaitToReadAsync();
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        await Task.WhenAll(producer, consumer);
    }
    
    private static async Task SpscChannel(int count, int capacity, bool allowSynchronousContinuations)
    {
        var queue = new SpscChannel<int>(capacity, allowSynchronousContinuations);
        
        var producer = Task.Factory.StartNew(() =>
        {
            var spin = new SpinWait();

            for (var i = 0; i < count; i++)
            {
                while (!queue.TryWrite(i))
                    spin.SpinOnce(10);
            }

            queue.TryComplete();
        }, TaskCreationOptions.LongRunning);


        var consumer = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (queue.TryRead(out _))
                    continue;

                if (queue.IsCompleted)
                    break;
                
                await queue.WaitToReadAsync();
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        await Task.WhenAll(producer, consumer);
    }
}