using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void InvalidCapacity_ThrowsArgumentOutOfRangeException(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new Deque<int>(capacity));

        [Fact]
        public void ValidCapacity_InitializesState()
        {
            var deque = new Deque<int>(4);

            Assert.Equal(4, deque.Capacity);
            Assert.Equal(0, deque.Count);
            Assert.True(deque.IsEmpty);
            Assert.False(deque.IsFull);
        }
    }
}
