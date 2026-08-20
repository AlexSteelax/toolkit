using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class SpscQueueBenchmarks
{
    [Benchmark(OperationsPerInvoke = Count)]
    public async Task SingleChannel()
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = true
        });
        
        var producer = Task.Factory.StartNew(async () =>
        {
            var writer = channel.Writer;

            for (var i = 0; i < Count; i++)
            {
                while (!writer.TryWrite(i))
                    await writer.WaitToWriteAsync();
            }

            writer.Complete();
        }, TaskCreationOptions.LongRunning).Unwrap();
        
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