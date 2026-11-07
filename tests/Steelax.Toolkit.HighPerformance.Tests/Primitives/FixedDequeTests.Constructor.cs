using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class FixedDequeTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void InvalidCapacity_ThrowsArgumentOutOfRangeException(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedDeque<int>(capacity));

        [Fact]
        public void ValidCapacity_InitializesState()
        {
            var deque = new FixedDeque<int>(4);

            Assert.Equal(4, deque.Capacity);
            Assert.Equal(0, deque.Count);
            Assert.True(deque.IsEmpty);
            Assert.False(deque.IsFull);
        }
    }
}
