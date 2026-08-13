using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class Peek
    {
        [Fact]
        public void PeekFirst_ReturnsFirst_WithoutRemoving()
        {
            var deque = new Deque<int>(3);
            deque.TryAddLast(1);
            deque.TryAddLast(2);

            Assert.True(deque.TryPeekFirst(out var first));
            Assert.Equal(1, first);
            Assert.Equal(2, deque.Count);
        }

        [Fact]
        public void PeekLast_ReturnsLast_WithoutRemoving()
        {
            var deque = new Deque<int>(3);
            deque.TryAddLast(1);
            deque.TryAddLast(2);

            Assert.True(deque.TryPeekLast(out var last));
            Assert.Equal(2, last);
            Assert.Equal(2, deque.Count);
        }

        [Fact]
        public void Peek_WhenEmpty_ReturnsFalse()
        {
            var deque = new Deque<int>(2);

            Assert.False(deque.TryPeekFirst(out _));
            Assert.False(deque.TryPeekLast(out _));
        }
    }
}
