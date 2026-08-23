using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SegmentedQueueTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Ctor_DefaultMinimumSize_CreatesEmptyQueue()
        {
            var queue = new SegmentedQueue<int>();

            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
        }

        [Fact]
        public void Ctor_AcceptsPositiveMinimumSize()
        {
            var queue = new SegmentedQueue<int>(1);

            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Ctor_WhenMinimumSizeNotPositive_Throws(int minimumSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedQueue<int>(minimumSize));
        }
    }
}
