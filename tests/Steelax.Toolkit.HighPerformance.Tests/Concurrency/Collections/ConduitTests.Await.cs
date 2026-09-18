using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

public static partial class ConduitTests
{
    /// <summary>Readiness-signal tests driven via the awaitable behavior.</summary>
    public sealed class Await
    {
        private const ConduitBehavior Both = ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter;

        [Fact]
        public async Task ConsumerWaitsUntilData_ThenReads()
        {
            var conduit = new Conduit<int>(4, Both);

            var wait = conduit.WaitToReadAsync();
            Assert.False(wait.IsCompleted); // no data yet — still pending

            Assert.True(conduit.TryWrite(42));

            Assert.True(await wait);

            Assert.True(conduit.TryRead(out var value));
            Assert.Equal(42, value);
        }

        [Fact]
        public async Task WriterWaitsUntilCapacity_ThenWrites()
        {
            var conduit = new Conduit<int>(1, Both);
            Assert.True(conduit.TryWrite(1));

            // Buffer is full — the writer must wait for a slot to free.
            var wait = conduit.WaitToWriteAsync();
            Assert.False(wait.IsCompleted); // still full — pending

            Assert.True(conduit.TryRead(out var first));
            Assert.Equal(1, first);

            Assert.True(await wait);

            Assert.True(conduit.TryWrite(2));
            Assert.True(conduit.TryRead(out var second));
            Assert.Equal(2, second);
        }

        [Fact]
        public async Task Complete_EndOfStreamWakesReader()
        {
            var conduit = new Conduit<int>(4, Both);

            var wait = conduit.WaitToReadAsync();
            Assert.False(wait.IsCompleted);

            Assert.True(conduit.TryComplete());

            // The reader wakes with false — the stream has ended.
            Assert.False(await wait);

            Assert.False(conduit.TryRead(out _));
            Assert.True(conduit.IsCompleted);
        }

        [Fact]
        public async Task WaitToReadAsync_AlreadyCompleted_ReturnsFalse()
        {
            var conduit = new Conduit<int>(4, Both);
            conduit.TryComplete();

            Assert.False(await conduit.WaitToReadAsync());
        }

        [Fact]
        public async Task WaitToWriteAsync_AlreadyCompleted_ReturnsFalse()
        {
            var conduit = new Conduit<int>(4, Both);
            conduit.TryComplete();

            Assert.False(await conduit.WaitToWriteAsync());
        }

        [Fact]
        public async Task WaitToReadAsync_DataAvailable_ReturnsTrue()
        {
            var conduit = new Conduit<int>(4, Both);
            Assert.True(conduit.TryWrite(1));

            Assert.True(await conduit.WaitToReadAsync());
        }

        [Fact(Timeout = 5000)]
        public async Task CompleteAfterData_WakesReaderToDrain()
        {
            var conduit = new Conduit<int>(4, Both);

            // Producer writes both items, WaitToWriteAsync, then completes in WriteSequence's finally.
            var filling = WriteSequence(
                conduit,
                new[] { 1, 2 },
                fallback: false,
                TestContext.Current.CancellationToken);

            // Terminate the stream after the data has had a chance to be written (edge trigger for reader).
            await Task.Delay(20, TestContext.Current.CancellationToken);
            conduit.TryComplete();

            var collected = await ReadAllAsync(conduit);

            await filling.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            Assert.Equal(new[] { 1, 2 }, collected);
            Assert.True(conduit.IsCompleted);
        }

        [Fact]
        public async Task BareWaitToRead_CoversLiveStatus()
        {
            // Without AwaitableReader the wait falls back to reporting liveness: true while alive.
            var conduit = new Conduit<int>(4);

            Assert.True(await conduit.WaitToReadAsync());
            Assert.True(await conduit.WaitToWriteAsync());

            conduit.TryComplete();

            Assert.False(await conduit.WaitToReadAsync());
            Assert.False(await conduit.WaitToWriteAsync());
        }

        [Fact(Timeout = 5000)]
        public async Task ProducerConsumer_BackgroundSequence_AllDelivered()
        {
            var conduit = new Conduit<int>(8, Both);

            var producer = WriteSequence(
                conduit,
                Enumerable.Range(0, 10_000),
                fallback: false,
                TestContext.Current.CancellationToken);

            var collected = await ReadAllAsync(conduit);

            await producer.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            Assert.Equal(Enumerable.Range(0, 10_000), collected);
            Assert.True(conduit.IsCompleted);
        }
    }
}
