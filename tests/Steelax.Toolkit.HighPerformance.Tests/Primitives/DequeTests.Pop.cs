using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class Pop
    {
        [Fact]
        public void PopFirst_FIFO_Order_Preserved()
        {
            var deque = new Deque<int>(4);
            deque.TryAddLast(1);
            deque.TryAddLast(2);
            deque.TryAddLast(3);

            Assert.True(deque.TryPopFirst(out var first));
            Assert.True(deque.TryPopFirst(out var second));
            Assert.True(deque.TryPopFirst(out var third));

            Assert.Equal(1, first);
            Assert.Equal(2, second);
            Assert.Equal(3, third);
            Assert.True(deque.IsEmpty);
        }

        [Fact]
        public void PopLast_LIFO_Order_Preserved()
        {
            var deque = new Deque<int>(4);
            deque.TryAddLast(1);
            deque.TryAddLast(2);
            deque.TryAddLast(3);

            Assert.True(deque.TryPopLast(out var last));
            Assert.True(deque.TryPopLast(out var middle));
            Assert.True(deque.TryPopLast(out var first));

            Assert.Equal(3, last);
            Assert.Equal(2, middle);
            Assert.Equal(1, first);
            Assert.True(deque.IsEmpty);
        }

        [Fact]
        public void PopFirst_WhenEmpty_ReturnsFalse()
        {
            var deque = new Deque<int>(2);

            Assert.False(deque.TryPopFirst(out var item));
            Assert.Equal(0, item);
        }

        [Fact]
        public void PopLast_WhenEmpty_ReturnsFalse()
        {
            var deque = new Deque<int>(2);

            Assert.False(deque.TryPopLast(out var item));
            Assert.Equal(0, item);
        }

        [Fact]
        public void Circular_ReusesSlots_AndPreservesOrder()
        {
            var deque = new Deque<int>(3);

            // Fill the ring.
            deque.TryAddLast(1);
            deque.TryAddLast(2);
            deque.TryAddLast(3);
            Assert.Equal(3, deque.Count);
            Assert.True(deque.IsFull);
            Assert.False(deque.TryAddLast(4));

            // Consume two — the ring must reuse those slots.
            Assert.True(deque.TryPopFirst(out _));
            Assert.True(deque.TryPopFirst(out _));

            // Add into the freed slots (this would fail for a non-circular buffer).
            Assert.True(deque.TryAddLast(4));
            Assert.True(deque.TryAddLast(5));

            // Strict FIFO across the wrap.
            Assert.True(deque.TryPopFirst(out var a));
            Assert.True(deque.TryPopFirst(out var b));
            Assert.True(deque.TryPopFirst(out var c));

            Assert.Equal(3, a);
            Assert.Equal(4, b);
            Assert.Equal(5, c);
            Assert.True(deque.IsEmpty);
        }

        [Fact]
        public void PopLast_ReusesSlots_FromFront()
        {
            var deque = new Deque<int>(3);

            deque.TryAddLast(1);
            deque.TryAddLast(2);
            deque.TryAddLast(3);

            // Remove from the back, then add to the front.
            Assert.True(deque.TryPopLast(out _));

            Assert.True(deque.TryAddFirst(0));

            Assert.True(deque.TryPopFirst(out var first));
            Assert.Equal(0, first);
            Assert.Equal(2, deque.Count);
        }
    }
}
