using System.Diagnostics;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

public static partial class ConduitTests
{
    /// <summary>Load tests exercising the core and readiness signals under contention.</summary>
    public sealed class Concurrency(ITestOutputHelper output)
    {
        [Fact]
        public async Task ConcurrentProducerConsumer_InputMatchesOutput()
        {
            const int count = 30_000;
            const int capacity = 512;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            
            cts.CancelAfter(3000);
            
            var watch = Stopwatch.StartNew();
            var conduit = new Conduit<int>(capacity, ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter);
            // var probe = new ProbeTracker(() => ChannelProbe<int>.Dump(conduit));
            // probe.Run();

            var producerState = new SequenceState();
            var consumerState = new SequenceState();
            var producer = WriteSequence(conduit, Enumerable.Range(0, count), fallback: false, cts.Token, producerState);
            var consumer = ReadAllAsync(conduit, consumerState);

            try
            {
                await producer.WaitAsync(cts.Token);
                
                var collected = await consumer.WaitAsync(cts.Token);;

                Assert.Equal(count, collected.Count);
                Assert.Equal(Enumerable.Range(0, count), collected);
            }
            catch (OperationCanceledException)
            {
                // probe.Stop();
                
                Assert.Fail("OperationCanceledException");
            }
            finally
            {
                watch.Stop();
                // probe.Stop();
                
                output.WriteLine($"Conduit count={conduit.Count} full={conduit.IsFull} completed={conduit.IsCompleted}");
                output.WriteLine($"Producer c={producerState.Count} | Consumer c={consumerState.Count}");

                // probe.ToOutput(output);
                output.WriteLine(watch.ElapsedMilliseconds is var elapsed && elapsed != 0 ? $"Time elapsed: {1m * count / elapsed:F3} item/ms" : "Time elapsed: - item/ms");
            }
        }

        [Fact(Timeout = 1000)]
        public async Task ConcurrentFault_WakesReaderWithException()
        {
            var conduit = new Conduit<int>(4, ConduitBehavior.AwaitableReader);
            var ex = new InvalidOperationException("producer failed");

            var consumer = Task.Run(async () =>
            {
                await conduit.WaitToReadAsync();
                return conduit.TryRead(out _);
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            conduit.TryComplete(ex);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await consumer);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 1000)]
        public async Task ConcurrentEmptyComplete_WakesReaderToEndOfStream()
        {
            var conduit = new Conduit<int>(4, ConduitBehavior.AwaitableReader);

            var consumer = Task.Run(async () =>
            {
                await conduit.WaitToReadAsync();
                _ = conduit.TryRead(out _);
                return conduit.IsCompleted;
            }, TestContext.Current.CancellationToken);

            await Task.Delay(50, TestContext.Current.CancellationToken);

            conduit.TryComplete();

            Assert.True(await consumer);
        }

        [Fact(Timeout = 5000)]
        public async Task ConcurrentTerminate_SpinningWriter_ThrowsOnWrite()
        {
            const int capacity = 4;
            var conduit = new Conduit<int>(capacity);
            var ex = new InvalidOperationException("watchdog abort");

            // Writer spins forever via Thread.Yield (no async wait): after TryTerminate, the next
            // TryWrite throws the termination exception.
            var writer = Task.Factory.StartNew(() =>
            {
                for (var i = 0; ; i++)
                {
                    while (!conduit.TryWrite(i))
                        Thread.Yield();
                }
            }, TaskCreationOptions.LongRunning);

            await Task.Delay(250, TestContext.Current.CancellationToken);
            Assert.True(conduit.TryTerminate(ex));

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await writer);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 5000)]
        public async Task ConcurrentTerminate_SpinningReader_ThrowsOnRead()
        {
            const int capacity = 4;
            var conduit = new Conduit<int>(capacity);
            var ex = new InvalidOperationException("watchdog abort");

            // Reader spins forever via Thread.Yield (no async wait): after TryTerminate, the next
            // TryRead throws the termination exception.
            var reader = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (conduit.TryRead(out _))
                        continue;

                    Thread.Yield();
                }
            }, TaskCreationOptions.LongRunning);

            await Task.Delay(250, TestContext.Current.CancellationToken);
            Assert.True(conduit.TryTerminate(ex));

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader);
            Assert.Same(ex, thrown);
        }

        [Fact(Timeout = 10000)]
        public async Task ProducerConsumer_AsyncSignals_InputMatchesOutput()
        {
            var conduit = new Conduit<int>(
                16,
                ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter);

            const int count = 2000;

            var producer = WriteSequence(conduit, Enumerable.Range(0, count), fallback: false, TestContext.Current.CancellationToken);
            var consumer = ReadAllAsync(conduit);

            var collected = await consumer;

            await producer.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(count, collected.Count);
            Assert.Equal(Enumerable.Range(0, count), collected);
        }
    }
}
