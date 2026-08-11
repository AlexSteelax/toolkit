using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class EventQueueBenchmarks
{
    [Benchmark(OperationsPerInvoke = 1_000)]
    public Task SingleChannel_Async_1k() => SingleChannel(1_000, Capacity, false);
    
    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public Task SingleChannel_Sync_1kk() => SingleChannel(1_000_000, Capacity, true);
    
    private static async Task SingleChannel(int count, int capacity, bool allowSynchronousContinuations)
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = allowSynchronousContinuations
        });
        
        var producer = Task.Factory.StartNew(() =>
        {
            var spin = new SpinWait();
            var writer = channel.Writer;

            for (var i = 0; i < count; i++)
            {
                while (!writer.TryWrite(i))
                    spin.SpinOnce();
            }

            writer.Complete();
        }, TaskCreationOptions.LongRunning);
        
        var consumer = Task.Factory.StartNew(async () =>
        {
            var reader = channel.Reader;

            while (true)
            {
                if (reader.TryRead(out _))
                    continue;
                
                if (!await reader.WaitToReadAsync())
                    break;
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        await Task.WhenAll(producer, consumer);
    }
}