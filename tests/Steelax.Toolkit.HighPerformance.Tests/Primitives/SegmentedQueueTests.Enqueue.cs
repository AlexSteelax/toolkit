using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SegmentedQueueTests
{
    public sealed class Enqueue
    {
        [Fact]
        public void Enqueue_ToEmptyQueue_SetsCountAndNotEmpty()
        {
            var queue = new SegmentedQueue<int>(4);

            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);

            Assert.Equal(3, queue.Count);
            Assert.False(queue.IsEmpty);
        }

        [Fact]
        public void Enqueue_BeyondSingleChunk_SpansMultipleChunks()
        {
            var queue = new SegmentedQueue<int>(4);

            for (var i = 0; i < 20; i++)
                queue.Enqueue(i);

            Assert.Equal(20, queue.Count);
            Assert.Equal(Enumerable.Range(0, 20), Drain(queue));
        }

        [Fact]
        public void Enqueue_AlwaysSucceeds_ForManyItems()
        {
            var queue = new SegmentedQueue<int>(2);

            for (var i = 0; i < 10_000; i++)
                queue.Enqueue(i);

            Assert.Equal(10_000, queue.Count);
        }

        [Fact]
        public void Enqueue_AfterFullDrain_WorksAgain()
        {
            var queue = new SegmentedQueue<int>(4);

            for (var i = 0; i < 10; i++)
                queue.Enqueue(i);

            Drain(queue);

            for (var i = 0; i < 10; i++)
                queue.Enqueue(100 + i);

            Assert.Equal(Enumerable.Range(100, 10), Drain(queue));
        }
    }
}
