using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SegmentedQueueTests
{
    public sealed class Peek
    {
        [Fact]
        public void Peek_OnEmpty_ReturnsFalse()
        {
            var queue = new SegmentedQueue<int>();

            Assert.False(queue.TryPeek(out var item));
            Assert.Equal(default, item);
        }

        [Fact]
        public void Peek_DoesNotConsume()
        {
            var queue = new SegmentedQueue<int>(4);

            queue.Enqueue(1);
            queue.Enqueue(2);

            Assert.True(queue.TryPeek(out var first));
            Assert.True(queue.TryPeek(out var second));
            Assert.Equal(1, first);
            Assert.Equal(1, second);
            Assert.Equal(2, queue.Count);

            Assert.True(queue.TryDequeue(out var item));
            Assert.Equal(1, item);
        }

        [Fact]
        public void Peek_AcrossChunkBoundary_ReturnsNextValue()
        {
            var queue = new SegmentedQueue<int>(2);

            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);

            queue.TryDequeue(out _);
            queue.TryDequeue(out _);

            Assert.True(queue.TryPeek(out var item));
            Assert.Equal(3, item);
        }
    }
}
