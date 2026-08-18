using System.Diagnostics;
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

public static partial class SpscQueueTests
{
    public sealed class Concurrency(ITestOutputHelper output)
    {
        [Theory(Timeout = 10000)]
        [InlineData(10_000, 1)]
        [InlineData(10_000, 4)]
        [InlineData(10_000, 32)]
        [InlineData(10_000, 512)]
        public async Task ConcurrentProducerConsumer_InputMatchesOutput(int count, int capacity)
        {
            var watch = Stopwatch.StartNew();
            var queue = new SpscQueue<int>(capacity);

            var producer = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                {
                    while (!queue.TryWrite(i))
                        Thread.Yield();
                }

                queue.TryComplete();
            }, TaskCreationOptions.LongRunning);

            var consumer = Task.Factory.StartNew(() => ReadAll(queue), TaskCreationOptions.LongRunning);

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
        public async Task ConcurrentFault_RethrownOnConsumer()
        {
            var queue = new SpscQueue<int>(4);
            var ex = new InvalidOperationException("producer failed");

            var consumer = Task.Run(() =>
            {
                while (true)
                {
                    if (queue.TryRead(out _))
                        continue;

                    if (queue.IsCompleted)
                        return false;

                    Thread.Yield();
                }
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            queue.TryComplete(ex);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await consumer);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 1000)]
        public async Task ConcurrentEmptyComplete_ConsumerSeesEndOfStream()
        {
            var queue = new SpscQueue<int>(4);

            var consumer = Task.Run(() =>
            {
                while (true)
                {
                    if (queue.TryRead(out _))
                        continue;

                    if (queue.IsCompleted)
                        return true;

                    Thread.Yield();
                }
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            queue.TryComplete();

            Assert.True(await consumer);
        }
    }
}
