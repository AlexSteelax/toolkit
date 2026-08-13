using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class Offset
    {
        [Fact]
        public void TryGetAt_ReturnsElementAtOffset()
        {
            var deque = new Deque<int>(4);
            deque.TryAddLast(1);
            deque.TryAddLast(2);
            deque.TryAddLast(3);

            Assert.True(deque.TryGetAt(0, out var first));
            Assert.True(deque.TryGetAt(1, out var mid));
            Assert.True(deque.TryGetAt(2, out var last));

            Assert.Equal(1, first);
            Assert.Equal(2, mid);
            Assert.Equal(3, last);
        }

        [Fact]
        public void TryGetAt_OutOfRange_ReturnsFalse()
        {
            var deque = new Deque<int>(3);
            deque.TryAddLast(1);

            Assert.False(deque.TryGetAt(1, out _));
            Assert.False(deque.TryGetAt(^2, out _));
        }

        [Fact]
        public void Indexer_ReturnsRef_ToOffsetElement()
        {
            var deque = new Deque<Counter>(4);
            deque.TryAddLast(new Counter());
            deque.TryAddLast(new Counter());
            deque.TryAddLast(new Counter());

            ref var mid = ref deque[1];
            mid.Value = 42;

            Assert.True(deque.TryGetAt(1, out var peeked));
            Assert.Equal(42, peeked.Value);
        }

        [Fact]
        public void Indexer_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var deque = new Deque<int>(2);
            deque.TryAddLast(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[^2]);
        }

        [Fact]
        public void IndexFromEnd_ReturnsRef_ToElement()
        {
            var deque = new Deque<Counter>(4);
            deque.TryAddLast(new Counter());
            deque.TryAddLast(new Counter());
            deque.TryAddLast(new Counter());

            // ^1 is the last element, ^2 the middle, ^Count the first.
            ref var last = ref deque[^1];
            last.Value = 30;

            ref var first = ref deque[^3];
            first.Value = 10;

            Assert.True(deque.TryPeekLast(out var peekedLast));
            Assert.Equal(30, peekedLast.Value);

            Assert.True(deque.TryPeekFirst(out var peekedFirst));
            Assert.Equal(10, peekedFirst.Value);
        }

        [Fact]
        public void IndexFromEnd_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var deque = new Deque<int>(2);
            deque.TryAddLast(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[^2]);
        }
    }
}
