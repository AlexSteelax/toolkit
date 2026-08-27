using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

public static partial class ConduitTests
{
    public sealed class Functional
    {
        [Fact]
        public void ReadInOrder_YieldsWrittenItems()
        {
            var conduit = new Conduit<int>(8);

            for (var i = 0; i < 5; i++)
                Assert.True(conduit.TryWrite(i));

            conduit.TryComplete();

            var collected = ReadAll(conduit);

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, collected);
        }

        [Fact]
        public void FullBuffer_RejectsWrites()
        {
            var conduit = new Conduit<int>(2);

            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryWrite(2));
            Assert.False(conduit.TryWrite(3));
        }

        [Fact]
        public void IsFull_TracksBufferOccupancy()
        {
            var conduit = new Conduit<int>(2);

            Assert.False(conduit.IsFull);

            Assert.True(conduit.TryWrite(1));
            Assert.False(conduit.IsFull);

            Assert.True(conduit.TryWrite(2));
            Assert.True(conduit.IsFull);

            Assert.True(conduit.TryRead(out _));
            Assert.False(conduit.IsFull);
        }

        [Fact]
        public void IsFull_IgnoresStreamCompletion()
        {
            // A closed stream that still holds buffered items is reported non-full (IsFull is purely
            // about occupancy, not write-availability); completion of an empty buffer leaves it non-full.
            var conduits = new Conduit<int>[2];
            conduits[0] = new Conduit<int>(2);
            conduits[1] = new Conduit<int>(2);

            Assert.True(conduits[0].TryWrite(1));
            Assert.True(conduits[0].TryWrite(2));
            Assert.True(conduits[1].TryWrite(1));   // [1] holds a single buffered item

            conduits[0].TryComplete();
            conduits[1].TryComplete();

            Assert.True(conduits[0].IsFull);        // both slots still full even though completed
            Assert.False(conduits[1].IsFull);       // only one buffered item
        }

        [Fact]
        public void EmptyRead_ReturnsFalse_WithoutCompletion()
        {
            var conduit = new Conduit<int>(4);

            Assert.False(conduit.TryRead(out _));
            Assert.False(conduit.IsCompleted);
            Assert.Equal(0, conduit.Count);
        }

        [Fact]
        public void Count_TracksBufferedItems()
        {
            var conduit = new Conduit<int>(4);

            Assert.Equal(0, conduit.Count);

            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryWrite(2));
            Assert.Equal(2, conduit.Count);

            Assert.True(conduit.TryRead(out _));
            Assert.Equal(1, conduit.Count);
        }

        [Fact]
        public void Complete_EmptyQueue_IsCompletedImmediately()
        {
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryComplete());

            Assert.True(conduit.IsCompleted);
            Assert.False(conduit.TryRead(out _));
        }

        [Fact]
        public void Complete_WithBufferedData_CompletesAfterDrain()
        {
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryWrite(2));

            // Closed, but not completed while data is still buffered.
            Assert.True(conduit.TryComplete());
            Assert.False(conduit.IsCompleted);

            var collected = ReadAll(conduit);

            Assert.Equal(new[] { 1, 2 }, collected);
            Assert.True(conduit.IsCompleted);
        }

        [Fact]
        public void Complete_Twice_SecondReturnsFalse()
        {
            var conduit = new Conduit<int>(4);

            Assert.True(conduit.TryComplete());
            Assert.False(conduit.TryComplete());
        }

        [Fact]
        public void Fault_RethrownOnTryRead()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("boom");

            Assert.True(conduit.TryComplete(ex));

            var thrown = Assert.Throws<InvalidOperationException>(() => conduit.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void Fault_WithBufferedData_ThrowsAfterDrain()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("boom");

            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryComplete(ex));

            // Buffered data is still readable before the fault surfaces.
            Assert.True(conduit.TryRead(out var value));
            Assert.Equal(1, value);

            var thrown = Assert.Throws<InvalidOperationException>(() => conduit.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void WriteAfterComplete_Throws()
        {
            var conduit = new Conduit<int>(4);
            conduit.TryComplete();

            Assert.Throws<InvalidOperationException>(() => conduit.TryWrite(1));
        }

        [Fact]
        public void WriteAfterCompleteWithFault_ThrowsOriginalException()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("boom");
            conduit.TryComplete(ex);

            var thrown = Assert.Throws<InvalidOperationException>(() => conduit.TryWrite(1));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void WriteAfterCompleteWithBufferedData_StillThrows()
        {
            // The closed gate must reject writes regardless of whether the buffer is empty:
            // a completed stream accepts no further writes even before its data is drained.
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryWrite(1));
            conduit.TryComplete();

            Assert.Throws<InvalidOperationException>(() => conduit.TryWrite(2));
        }

        [Fact]
        public void OnReadReady_RaisedOnEmptyToNonEmptyTransition_Only()
        {
            var readCount = 0;
            var conduit = new Conduit<int>(4);
            conduit.OnReadReady += () => readCount++;

            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryWrite(2));     // buffer not empty before write → no event
            Assert.Equal(1, readCount);

            Assert.True(conduit.TryRead(out _));
            Assert.True(conduit.TryRead(out _));  // buffer drained

            Assert.True(conduit.TryWrite(3));     // empty → non-empty again → event
            Assert.Equal(2, readCount);
        }

        [Fact]
        public void TryPeek_DoesNotConsume()
        {
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryWrite(2));

            Assert.True(conduit.TryPeek(out var head));
            Assert.Equal(1, head);
            Assert.Equal(2, conduit.Count);   // head not consumed

            Assert.True(conduit.TryRead(out var value));
            Assert.Equal(1, value);
        }

        [Fact]
        public void TryPeek_Repeated_ReturnsSameHead()
        {
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryWrite(42));

            Assert.True(conduit.TryPeek(out var first));
            Assert.True(conduit.TryPeek(out var second));
            Assert.Equal(42, first);
            Assert.Equal(42, second);
            Assert.Equal(1, conduit.Count);
        }

        [Fact]
        public void TryPeek_EmptyBuffer_ReturnsFalse_WithoutCompletion()
        {
            var conduit = new Conduit<int>(4);

            Assert.False(conduit.TryPeek(out _));
            Assert.False(conduit.IsCompleted);
            Assert.Equal(0, conduit.Count);
        }

        [Fact]
        public void TryPeek_AfterCompleteWithBufferedData_ReturnsHead()
        {
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryWrite(7));
            conduit.TryComplete();

            // Completion does not destroy buffered data — the head is still peekable.
            Assert.True(conduit.TryPeek(out var head));
            Assert.Equal(7, head);
        }

        [Fact]
        public void TryPeek_CompleteEmptyQueue_IsCompleted()
        {
            var conduit = new Conduit<int>(4);
            Assert.True(conduit.TryComplete());

            Assert.False(conduit.TryPeek(out _));
            Assert.True(conduit.IsCompleted);
        }

        [Fact]
        public void TryPeek_FaultedEmptyQueue_RethrowsOnPeek()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("boom");
            Assert.True(conduit.TryComplete(ex));

            var thrown = Assert.Throws<InvalidOperationException>(() => conduit.TryPeek(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void TryTerminate_CompletesImmediatelyWithError()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("abort");

            // Hard abort: even with buffered data, the reader sees the exception immediately.
            Assert.True(conduit.TryWrite(1));
            Assert.True(conduit.TryWrite(2));
            Assert.True(conduit.TryTerminate(ex));
            Assert.True(conduit.IsCompleted);

            var thrown = Assert.Throws<InvalidOperationException>(() => conduit.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void TryTerminate_AfterComplete_IsRejected()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("abort");

            // Once terminally completed, a further terminate is a no-op returning false.
            Assert.True(conduit.TryTerminate(ex));
            Assert.False(conduit.TryTerminate(ex));
            Assert.True(conduit.IsCompleted);
        }

        [Fact]
        public void TryTerminate_EmptyBuffer_CompletesAndThrowsOnRead()
        {
            var conduit = new Conduit<int>(4);
            var ex = new InvalidOperationException("abort");

            Assert.True(conduit.TryTerminate(ex));
            Assert.True(conduit.IsCompleted);

            var thrown = Assert.Throws<InvalidOperationException>(() => conduit.TryRead(out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void OnWriteReady_RaisedWhenFullSlotFreed()
        {
            var writeCount = 0;
            var conduit = new Conduit<int>(1);
            conduit.OnWriteReady += () => writeCount++;

            Assert.True(conduit.TryWrite(1));   // fills the buffer
            Assert.Equal(0, writeCount);        // not full→free yet

            Assert.True(conduit.TryRead(out _)); // frees the only slot → Writable
            Assert.Equal(1, writeCount);
        }

        [Fact]
        public void OnWriteReady_NotRaisedWhenNotFull()
        {
            var writeCount = 0;
            var conduit = new Conduit<int>(4);
            conduit.OnWriteReady += () => writeCount++;

            Assert.True(conduit.TryWrite(1));   // buffer not full before write
            Assert.True(conduit.TryRead(out _)); // freeing a non-full slot is not "full → free"
            Assert.Equal(0, writeCount);
        }

        [Fact]
        public void OnReadReady_AfterComplete_NotifiesReader()
        {
            var readReady = false;
            var conduit = new Conduit<int>(4);
            conduit.OnReadReady += () => readReady = true;

            // Completing wakes the reader to re-check the queue.
            Assert.True(conduit.TryComplete());
            Assert.True(readReady);
        }
    }
}
