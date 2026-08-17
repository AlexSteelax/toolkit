using System.Diagnostics;
using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class SpscChannelTests
{
    /// <summary>Load tests exercising the read/write readiness signals under contention.</summary>
    public sealed class Concurrency(ITestOutputHelper output)
    {
        [Theory(Timeout = 10000)]
        [InlineData(20, 1, true)]
        [InlineData(50, 4, true)]
        [InlineData(200, 32, false)]
        [InlineData(3000, 512, true)]
        public async Task ConcurrentProducerConsumer_InputMatchesOutput(int count, int capacity, bool allowSynchronousContinuations)
        {
            var watch = Stopwatch.StartNew();
            var channel = new SpscChannel<int>(capacity, allowSynchronousContinuations);

            var producer = Task.Factory.StartNew(() =>
            {
                var spin = new SpinWait();

                for (var i = 0; i < count; i++)
                {
                    while (!channel.TryWrite(i))
                        spin.SpinOnce(10);
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
    }
}
