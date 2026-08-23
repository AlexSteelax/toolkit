using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SegmentedQueueTests
{
    public sealed class Dequeue
    {
        [Fact]
        public void Dequeue_FromEmpty_ReturnsFalse()
        {
            var queue = new SegmentedQueue<int>();

            Assert.False(queue.TryDequeue(out var item));
            Assert.Equal(default, item);
        }

        [Fact]
        public void Dequeue_PreservesFifoOrder()
        {
            var queue = new SegmentedQueue<int>(3);

            for (var i = 0; i < 100; i++)
                queue.Enqueue(i);

            Assert.Equal(Enumerable.Range(0, 100), Drain(queue));
            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
        }

        [Fact]
        public void Dequeue_AcrossChunkBoundary_ReturnsInOrder()
        {
            var queue = new SegmentedQueue<int>(3);

            for (var i = 0; i < 10; i++)
                queue.Enqueue(i);

            var expected = 0;

            while (queue.TryDequeue(out var item))
            {
                Assert.Equal(expected, item);
                expected++;
            }

            Assert.Equal(10, expected);
            Assert.True(queue.IsEmpty);
        }

        [Fact]
        public void Dequeue_AfterLastItem_ReturnsFalse()
        {
            var queue = new SegmentedQueue<int>(4);

            queue.Enqueue(1);
            queue.Enqueue(2);

            Assert.True(queue.TryDequeue(out _));
            Assert.True(queue.TryDequeue(out _));
            Assert.False(queue.TryDequeue(out _));
        }

        [Fact]
        public void Dequeue_CountTracksRemoval()
        {
            var queue = new SegmentedQueue<int>(2);

            for (var i = 0; i < 5; i++)
                queue.Enqueue(i);

            Assert.Equal(5, queue.Count);

            queue.TryDequeue(out _);
            queue.TryDequeue(out _);

            Assert.Equal(3, queue.Count);
            Assert.False(queue.IsEmpty);
        }

        [Fact]
        public void Dequeue_ReferenceType_ReturnsItems()
        {
            var queue = new SegmentedQueue<string?>(2);

            queue.Enqueue("a");
            queue.Enqueue("b");

            Assert.True(queue.TryDequeue(out var first));
            Assert.Equal("a", first);

            Assert.True(queue.TryDequeue(out var second));
            Assert.Equal("b", second);

            Assert.False(queue.TryDequeue(out var none));
            Assert.Null(none);
        }
    }
}
