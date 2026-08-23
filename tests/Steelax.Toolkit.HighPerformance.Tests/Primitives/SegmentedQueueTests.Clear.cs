using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SegmentedQueueTests
{
    public sealed class Clear
    {
        [Fact]
        public void Clear_OnEmptyQueue_IsNoOp()
        {
            var queue = new SegmentedQueue<int>();

            queue.Clear();

            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
        }

        [Fact]
        public void Clear_RemovesAllElements()
        {
            var queue = new SegmentedQueue<int>(4);

            for (var i = 0; i < 20; i++)
                queue.Enqueue(i);

            queue.Clear();

            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
            Assert.False(queue.TryDequeue(out _));
            Assert.False(queue.TryPeek(out _));
        }

        [Fact]
        public void Clear_AllowsReuse()
        {
            var queue = new SegmentedQueue<int>(2);

            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);

            queue.Clear();
            queue.Enqueue(42);

            Assert.Equal(1, queue.Count);
            Assert.True(queue.TryDequeue(out var item));
            Assert.Equal(42, item);
        }

        [Fact]
        public void Clear_ReferenceType_Works()
        {
            var queue = new SegmentedQueue<string>(2);

            queue.Enqueue("a");
            queue.Enqueue("b");
            queue.Enqueue("c");

            queue.Clear();

            Assert.True(queue.IsEmpty);
            queue.Enqueue("d");
            Assert.True(queue.TryDequeue(out var item));
            Assert.Equal("d", item);
        }
    }
}
