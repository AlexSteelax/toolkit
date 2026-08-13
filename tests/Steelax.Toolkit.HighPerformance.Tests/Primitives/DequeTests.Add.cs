using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class Add
    {
        [Fact]
        public void AddLast_WhenFull_ReturnsFalse()
        {
            var deque = new Deque<int>(2);

            Assert.True(deque.TryAddLast(1));
            Assert.True(deque.TryAddLast(2));
            Assert.False(deque.TryAddLast(3));
            Assert.Equal(2, deque.Count);
        }

        [Fact]
        public void AddFirst_WhenFull_ReturnsFalse()
        {
            var deque = new Deque<int>(2);

            Assert.True(deque.TryAddFirst(1));
            Assert.True(deque.TryAddFirst(2));
            Assert.False(deque.TryAddFirst(3));
            Assert.Equal(2, deque.Count);
        }

        [Fact]
        public void AddFirst_AddsToFront()
        {
            var deque = new Deque<int>(4);

            deque.TryAddLast(1);
            deque.TryAddLast(2);
            deque.TryAddFirst(0);

            Assert.True(deque.TryPeekFirst(out var first));
            Assert.Equal(0, first);
            Assert.Equal(3, deque.Count);
        }

        [Fact]
        public void Count_ReflectsAddedElements()
        {
            var deque = new Deque<int>(4);

            deque.TryAddLast(1);
            deque.TryAddLast(2);

            Assert.Equal(2, deque.Count);
            Assert.False(deque.IsEmpty);
        }
    }
}
