using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class Clear
    {
        [Fact]
        public void Clear_ResetsDeque()
        {
            var deque = new Deque<int>(3);
            deque.TryAddLast(1);
            deque.TryAddLast(2);

            deque.Clear();

            Assert.Equal(0, deque.Count);
            Assert.True(deque.IsEmpty);
            Assert.False(deque.TryPopFirst(out _));
        }
    }
}
