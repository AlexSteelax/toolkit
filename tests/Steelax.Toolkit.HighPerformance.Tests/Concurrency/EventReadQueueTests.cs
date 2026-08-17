using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

/// <summary>Tests for the reader-subscription variant <see cref="EventReadQueue{T}"/>.</summary>
public static class EventReadQueueTests
{
    public sealed class Subscription
    {
        [Fact]
        public async Task OnReadReady_RaisedOnFirstInsert()
        {
            var queue = new EventReadQueue<int>(4);
            var raised = 0;
            queue.OnReadReady += () => raised++;

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2)); // buffer not empty before → no hook

            Assert.Equal(1, raised);

            Assert.True(queue.TryRead(out _));
            Assert.True(queue.TryRead(out _)); // drained

            Assert.True(queue.TryWrite(3)); // empty → non-empty edge again
            Assert.Equal(2, raised);
        }

        [Fact]
        public async Task OnReadReady_RaisedOnComplete()
        {
            var queue = new EventReadQueue<int>(4);
            var raised = 0;
            queue.OnReadReady += () => raised++;

            Assert.True(queue.TryComplete());
            Assert.Equal(1, raised);
        }

        [Fact]
        public async Task OnReadReady_WakesSubscriptionToDrain()
        {
            var queue = new EventReadQueue<int>(1);
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var collected = new List<int>();
            queue.OnReadReady += () => signal.TrySetResult();

            Assert.True(queue.TryWrite(1));

            // Reader may observe the event side; drain what is available.
            await signal.Task.WaitAsync(TestContext.Current.CancellationToken);
            if (queue.TryRead(out var value))
                collected.Add(value);

            Assert.Equal(new[] { 1 }, collected);
        }

        [Fact]
        public async Task WriterWaitsUntilCapacity_ThenWrites()
        {
            var queue = new EventReadQueue<int>(1);
            Assert.True(queue.TryWrite(1));

            var wait = queue.WaitToWriteAsync();
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted); // still full — pending

            Assert.True(queue.TryRead(out _));
            await wait;

            Assert.True(queue.TryWrite(2));
        }
    }
}
