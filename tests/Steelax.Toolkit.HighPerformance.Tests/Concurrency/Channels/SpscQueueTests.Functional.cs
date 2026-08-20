using System.Diagnostics.CodeAnalysis;
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

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
        public void OnReadable_RaisedOnEmptyToNonEmptyTransition_Only()
        {
            var queue = new TrackingQueue<int>(4);

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));     // buffer not empty before write → no hook
            Assert.Equal(1, queue.ReadableCount);

            Assert.True(queue.TryRead(out _));
            Assert.True(queue.TryRead(out _));  // buffer drained

            Assert.True(queue.TryWrite(3));     // empty → non-empty again → hook
            Assert.Equal(2, queue.ReadableCount);
        }

        [Fact]
        public void TryPeek_DoesNotConsume()
        {
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));

            Assert.True(queue.TryPeek(out var head));
            Assert.Equal(1, head);
            Assert.Equal(2, queue.Count);   // head not consumed

            Assert.True(queue.TryRead(out var value));
            Assert.Equal(1, value);
        }

        [Fact]
        public void TryPeek_Repeated_ReturnsSameHead()
        {
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryWrite(42));

            Assert.True(queue.TryPeek(out var first));
            Assert.True(queue.TryPeek(out var second));
            Assert.Equal(42, first);
            Assert.Equal(42, second);
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void TryPeek_EmptyBuffer_ReturnsFalse_WithoutCompletion()
        {
            var queue = new SpscQueue<int>(4);

            Assert.False(queue.TryPeek(out _));
            Assert.False(queue.IsCompleted);
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void TryPeek_AfterCompleteWithBufferedData_ReturnsHead()
        {
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryWrite(7));
            queue.TryComplete();

            // Completion does not destroy buffered data — the head is still peekable.
            Assert.True(queue.TryPeek(out var head));
            Assert.Equal(7, head);
        }

        [Fact]
        public void TryPeek_CompleteEmptyQueue_IsCompleted()
        {
            var queue = new SpscQueue<int>(4);
            Assert.True(queue.TryComplete());

            Assert.False(queue.TryPeek(out _));
            Assert.True(queue.IsCompleted);
        }

        [Fact]
        public void TryPeek_FaultedEmptyQueue_RethrowsOnPeek()
        {
            var queue = new SpscQueue<int>(4);
            var ex = new InvalidOperationException("boom");
            Assert.True(queue.TryComplete(ex));

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryPeek(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void TryTerminate_CompletesImmediatelyWithError()
        {
            var queue = new TerminatingQueue<int>(4);
            var ex = new InvalidOperationException("abort");

            // Hard abort: even with buffered data, the reader sees the exception immediately.
            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));
            Assert.True(queue.TryTerminate(ex));
            Assert.True(queue.IsCompleted);

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void TryTerminate_AfterComplete_IsRejected()
        {
            var queue = new TerminatingQueue<int>(4);
            var ex = new InvalidOperationException("abort");

            // Once terminally completed, a further terminate is a no-op returning false.
            Assert.True(queue.TryTerminate(ex));
            Assert.False(queue.TryTerminate(ex));
            Assert.True(queue.IsCompleted);
        }

        [Fact]
        public void TryTerminate_EmptyBuffer_CompletesAndThrowsOnRead()
        {
            var queue = new TerminatingQueue<int>(4);
            var ex = new InvalidOperationException("abort");

            Assert.True(queue.TryTerminate(ex));
            Assert.True(queue.IsCompleted);

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        private sealed class TrackingQueue<T>(int capacity) : SpscCoreQueue<T>(capacity)
        {
            public int ReadableCount { get; private set; }

            protected override void OnReadable() => ReadableCount++;
            
            public new bool TryWrite(T item) => base.TryWrite(item);
            
            public new bool TryRead([MaybeNullWhen(false)] out T value) => base.TryRead(out value);
        }

        private sealed class TerminatingQueue<T>(int capacity) : SpscCoreQueue<T>(capacity)
        {
            public new bool TryWrite(T item) => base.TryWrite(item);
            
            public new bool TryRead([MaybeNullWhen(false)] out T value) => base.TryRead(out value);

            public new bool TryTerminate(Exception ex) => base.TryTerminate(ex);
        }
    }
}
