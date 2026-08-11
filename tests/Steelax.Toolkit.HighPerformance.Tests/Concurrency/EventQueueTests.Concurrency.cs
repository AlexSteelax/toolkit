using System.Diagnostics;
using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class EventQueueTests
{
    public sealed class Concurrency(ITestOutputHelper output)
    {
        [Fact(Timeout = 10000)]
        public async Task ConcurrentProducerConsumer_SmallLimit_NoLoss()
        {
            const int count = 500;
            var queue = new EventQueue<int>(1, true);

            var producer = Task.Run(() =>
            {
                var spin = new SpinWait();

                for (var i = 0; i < count; i++)
                {
                    while (!queue.TryWrite(i))
                        spin.SpinOnce();
                }

                queue.Complete();
            }, TestContext.Current.CancellationToken);

            var collected = await ReadAllAsync(queue);

            await producer.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);

            Assert.Equal(count, collected.Count);
            Assert.Equal((long)count * (count - 1) / 2, collected.Sum(x => (long)x));
        }

        [Theory(Timeout = 10000)]
        [InlineData(1_000_000, 1, true)]
        [InlineData(100, 1, false)]
        [InlineData(1_000_000, 4, true)]
        [InlineData(1_000, 4, false)]
        [InlineData(1_000_000, 32, true)]
        [InlineData(1_000, 32, false)]
        [InlineData(1_000_000, 128, true)]
        [InlineData(1_000, 128, false)]
        public async Task ConcurrentProducerConsumer_InputMatchesOutput(int count, int capacity, bool allowSynchronousContinuations)
        {
            var watch = Stopwatch.StartNew();
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

            var consumer = Task.Factory.StartNew(() => ReadAllAsync(queue), TaskCreationOptions.LongRunning).Unwrap();

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
            var queue = new EventQueue<int>(4);
            var ex = new InvalidOperationException("producer failed");

            var consumer = Task.Run(async () =>
            {
                await queue.WaitToReadAsync();
                return queue.TryRead(out _, out _);
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            queue.Complete(ex);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await consumer);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 1000)]
        public async Task ConcurrentEmptyComplete_ConsumerSeesEndOfStream()
        {
            var queue = new EventQueue<int>(4);

            var consumer = Task.Run(async () =>
            {
                await queue.WaitToReadAsync();
                _ = queue.TryRead(out _, out var completed);
                return completed;
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            queue.Complete();

            Assert.True(await consumer);
        }
    }
}
