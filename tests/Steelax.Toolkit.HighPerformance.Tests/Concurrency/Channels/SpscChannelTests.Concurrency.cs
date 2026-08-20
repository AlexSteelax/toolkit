using System.Diagnostics;
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

public static partial class SpscChannelTests
{
    /// <summary>Load tests exercising the read/write readiness signals under contention.</summary>
    public sealed class Concurrency(ITestOutputHelper output)
    {
        [Theory(Timeout = 1000)]
        [InlineData(200, 1)]
        [InlineData(500, 4)]
        [InlineData(2000, 16)]
        [InlineData(30000, 512)]
        public async Task ConcurrentProducerConsumer_InputMatchesOutput(int count, int capacity)
        {
            var watch = Stopwatch.StartNew();
            var channel = new SpscChannel<int>(capacity);

            var producer = Task.Factory.StartNew(async () =>
            {
                for (var i = 0; i < count; i++)
                {
                    while (!channel.TryWrite(i))
                        if (!await channel.WaitToWriteAsync())
                            break;
                }

                channel.TryComplete();
            }, TaskCreationOptions.LongRunning);

            var consumer = Task.Factory.StartNew(() => ReadAllAsync(channel), TaskCreationOptions.LongRunning).Unwrap();

            try
            {
                await Task.WhenAll(producer, consumer).WaitAsync(TestContext.Current.CancellationToken);

                var collected = await consumer.WaitAsync(TestContext.Current.CancellationToken);

                Assert.Equal(count, collected.Count);
                Assert.Equal(Enumerable.Range(0, count), collected);
            }
            finally
            {
                watch.Stop();

                output.WriteLine(watch.ElapsedMilliseconds is var elapsed && elapsed != 0 ? $"Time elapsed: {1m * count / elapsed:F3} item/ms" : "Time elapsed: - item/ms");
            }
        }

        [Fact(Timeout = 1000)]
        public async Task ConcurrentFault_WakesReaderWithException()
        {
            var channel = new SpscChannel<int>(4);
            var ex = new InvalidOperationException("producer failed");

            var consumer = Task.Run(async () =>
            {
                await channel.WaitToReadAsync();
                return channel.TryRead(out _);
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            channel.TryComplete(ex);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await consumer);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 1000)]
        public async Task ConcurrentEmptyComplete_WakesReaderToEndOfStream()
        {
            var channel = new SpscChannel<int>(4);

            var consumer = Task.Run(async () =>
            {
                await channel.WaitToReadAsync();
                _ = channel.TryRead(out _);
                return channel.IsCompleted;
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            channel.TryComplete();

            Assert.True(await consumer);
        }

        [Fact(Timeout = 5000)]
        public async Task ConcurrentTerminate_SpinningWriter_ThrowsOnWrite()
        {
            const int capacity = 4;
            var channel = new SpscChannel<int>(capacity);
            var ex = new InvalidOperationException("watchdog abort");

            // Writer spins forever via Thread.Yield (no async wait): after TryTerminate, the next
            // TryWrite throws the termination exception.
            var writer = Task.Factory.StartNew(() =>
            {
                for (var i = 0; ; i++)
                {
                    while (!channel.TryWrite(i))
                        Thread.Yield();
                }
            }, TaskCreationOptions.LongRunning);

            await Task.Delay(250, TestContext.Current.CancellationToken);
            Assert.True(channel.TryTerminate(ex));

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await writer);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 5000)]
        public async Task ConcurrentTerminate_SpinningReader_ThrowsOnRead()
        {
            const int capacity = 4;
            var channel = new SpscChannel<int>(capacity);
            var ex = new InvalidOperationException("watchdog abort");

            // Reader spins forever via Thread.Yield (no async wait): after TryTerminate, the next
            // TryRead throws the termination exception.
            var reader = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (channel.TryRead(out _))
                        continue;

                    Thread.Yield();
                }
            }, TaskCreationOptions.LongRunning);

            await Task.Delay(250, TestContext.Current.CancellationToken);
            Assert.True(channel.TryTerminate(ex));

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader);
            Assert.Same(ex, thrown);
        }
    }
}
