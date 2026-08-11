using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class Dequeue
    {
        [Fact]
        public void FIFO_Order_Preserved()
        {
            var buffer = new RingBuffer<int>(4);
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);
            buffer.TryEnqueue(3);

            Assert.True(buffer.TryDequeue(out var first));
            Assert.True(buffer.TryDequeue(out var second));
            Assert.True(buffer.TryDequeue(out var third));

            Assert.Equal(1, first);
            Assert.Equal(2, second);
            Assert.Equal(3, third);
            Assert.True(buffer.IsEmpty);
        }

        [Fact]
        public void Dequeue_WhenEmpty_ReturnsFalse()
        {
            var buffer = new RingBuffer<int>(2);

            Assert.False(buffer.TryDequeue(out var item));
            Assert.Equal(0, item);
        }

        [Fact]
        public void Circular_ReusesSlots_AndPreservesOrder()
        {
            var buffer = new RingBuffer<int>(3);

            // Fill the ring.
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);
            buffer.TryEnqueue(3);
            Assert.True(buffer.IsFull);

            // Consume two — the ring must reuse those slots.
            Assert.True(buffer.TryDequeue(out _));
            Assert.True(buffer.TryDequeue(out _));

            // Enqueue into the freed slots (this would fail for a non-circular buffer).
            Assert.True(buffer.TryEnqueue(4));
            Assert.True(buffer.TryEnqueue(5));

            // Strict FIFO across the wrap.
            Assert.True(buffer.TryDequeue(out var a));
            Assert.True(buffer.TryDequeue(out var b));
            Assert.True(buffer.TryDequeue(out var c));

            Assert.Equal(3, a);
            Assert.Equal(4, b);
            Assert.Equal(5, c);
            Assert.True(buffer.IsEmpty);
        }
    }
}