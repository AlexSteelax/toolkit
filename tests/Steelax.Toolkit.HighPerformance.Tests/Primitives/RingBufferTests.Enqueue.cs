using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class Enqueue
    {
        [Fact]
        public void Enqueue_WhenFull_ReturnsFalse()
        {
            var buffer = new RingBuffer<int>(2);

            Assert.True(buffer.TryEnqueue(1));
            Assert.True(buffer.TryEnqueue(2));
            Assert.False(buffer.TryEnqueue(3));
            Assert.True(buffer.IsFull);
        }

        [Fact]
        public void Count_ReflectsEnqueuedElements()
        {
            var buffer = new RingBuffer<int>(4);

            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);

            Assert.Equal(2, buffer.Count);
            Assert.False(buffer.IsEmpty);
        }
    }
}