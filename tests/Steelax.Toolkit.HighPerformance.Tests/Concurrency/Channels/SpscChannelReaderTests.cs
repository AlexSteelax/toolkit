using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

/// <summary>Tests for the writer-subscription variant <see cref="SpscChannelReader{T}"/>.</summary>
public static class SpscChannelReaderTests
{
    public sealed class Subscription
    {
        [Fact]
        public void OnWriteReady_RaisedWhenFullSlotFreed()
        {
            var queue = new SpscChannelReader<int>(1);
            Assert.True(queue.TryWrite(1));

            var raised = 0;
            queue.OnWriteReady += () => raised++;

            Assert.True(queue.TryRead(out _));

            Assert.Equal(1, raised);
        }

        [Fact]
        public void OnWriteReady_NotRaisedWhenNotFull()
        {
            var queue = new SpscChannelReader<int>(4);
            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));

            var raised = 0;
            queue.OnWriteReady += () => raised++;

            // Buffer was not full before these reads → no capacity-freed event.
            Assert.True(queue.TryRead(out _));
            Assert.True(queue.TryRead(out _));

            Assert.Equal(0, raised);
        }

        [Fact]
        public void OnWriteReady_EdgeTriggeredOnFullToFreeTransition()
        {
            var queue = new SpscChannelReader<int>(1);
            Assert.True(queue.TryWrite(1));

            var raised = 0;
            queue.OnWriteReady += () => raised++;

            Assert.True(queue.TryRead(out _)); // full → free: raises
            _ = queue.TryRead(out _);          // empty (not full before): no raise

            Assert.True(queue.TryWrite(2));    // refill
            Assert.True(queue.TryRead(out _)); // full → free again: raises

            Assert.Equal(2, raised);
        }

        [Fact]
        public async Task ReaderWaitsUntilData_ThenReads()
        {
            var queue = new SpscChannelReader<int>(4);

            var wait = queue.WaitToReadAsync();
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted); // no data yet — pending

            Assert.True(queue.TryWrite(42));
            await wait;

            Assert.True(queue.TryRead(out var value));
            Assert.Equal(42, value);
        }
    }
}
