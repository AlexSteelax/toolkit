using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class SpscQueueTests
{
    public sealed class Functional
    {
        [Fact]
        public void ReadInOrder_YieldsWrittenItems()
        {
            var queue = new SpscQueue<int>(8);

            for (var i = 0; i < 5; i++)
                Assert.True(queue.TryWrite(i));

            var collected = new List<int>();
            while (queue.TryRead(out var value))
                collected.Add(value);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, collected);
        }

        [Fact]
        public void FullBuffer_RejectsWrites()
        {
            var queue = new SpscQueue<int>(2);

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));
            Assert.False(queue.TryWrite(3));
        }

        [Fact]
        public void EmptyRead_ReturnsFalse_WithoutCompletion()
        {
            var queue = new SpscQueue<int>(4);

            Assert.False(queue.TryRead(out _));
            Assert.False(queue.IsCompleted);
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void Count_TracksBufferedItems()
        {
            var queue = new SpscQueue<int>(4);

            Assert.Equal(0, queue.Count);

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));
            Assert.Equal(2, queue.Count);

            Assert.True(queue.TryRead(out _));
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void Complete_EmptyQueue_IsCompletedImmediately()
        {
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryComplete());

            Assert.True(queue.IsCompleted);
            Assert.False(queue.TryRead(out _));
        }

        [Fact]
        public void Complete_WithBufferedData_CompletesAfterDrain()
        {
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));

            // Closed, but not completed while data is still buffered.
            Assert.True(queue.TryComplete());
            Assert.False(queue.IsCompleted);

            var collected = ReadAll(queue);

            Assert.Equal(new[] { 1, 2 }, collected);
            Assert.True(queue.IsCompleted);
        }

        [Fact]
        public void Complete_Twice_SecondReturnsFalse()
        {
            var queue = new SpscQueue<int>(4);

            Assert.True(queue.TryComplete());
            Assert.False(queue.TryComplete());
        }

        [Fact]
        public void Fault_RethrownOnTryRead()
        {
            var queue = new SpscQueue<int>(4);
            var ex = new InvalidOperationException("boom");

            Assert.True(queue.TryComplete(ex));

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void Fault_WithBufferedData_ThrowsAfterDrain()
        {
            var queue = new SpscQueue<int>(4);
            var ex = new InvalidOperationException("boom");

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryComplete(ex));

            // Buffered data is still readable before the fault surfaces.
            Assert.True(queue.TryRead(out var value));
            Assert.Equal(1, value);

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void WriteAfterComplete_Throws()
        {
            var queue = new SpscQueue<int>(4);
            queue.TryComplete();

            Assert.Throws<InvalidOperationException>(() => queue.TryWrite(1));
        }

        [Fact]
        public void WriteAfterCompleteWithFault_ThrowsOriginalException()
        {
            var queue = new SpscQueue<int>(4);
            var ex = new InvalidOperationException("boom");
            queue.TryComplete(ex);

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryWrite(1));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void WriteAfterCompleteWithBufferedData_StillThrows()
        {
            // The closed gate must reject writes regardless of whether the buffer is empty:
            // a completed stream accepts no further writes even before its data is drained.
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryWrite(1));
            queue.TryComplete();

            Assert.Throws<InvalidOperationException>(() => queue.TryWrite(2));
        }

        [Fact]
        public void OnFirstInsert_RaisedOnEmptyToNonEmptyTransition_Only()
        {
            var queue = new TrackingQueue<int>(4);

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));     // buffer not empty before write → no hook
            Assert.Equal(1, queue.FirstInsertCount);

            Assert.True(queue.TryRead(out _));
            Assert.True(queue.TryRead(out _));  // buffer drained

            Assert.True(queue.TryWrite(3));     // empty → non-empty again → hook
            Assert.Equal(2, queue.FirstInsertCount);
        }

        private sealed class TrackingQueue<T>(int capacity) : SpscQueue<T>(capacity)
        {
            public int FirstInsertCount { get; private set; }

            protected override void OnFirstInsertOrComplete() => FirstInsertCount++;
        }
    }
}
