using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class NonPowerOfTwoCapacity
    {
        [Fact]
        public void Capacity5_IsStrictBound_WhileSlotsRoundUpToPowerOfTwo()
        {
            var deque = new Deque<int>(5);

            // Capacity is the requested bound; the internal buffer rounds up to 8.
            Assert.Equal(5, deque.Capacity);

            for (var i = 0; i < 5; i++)
                Assert.True(deque.TryAddLast(i));
            Assert.Equal(5, deque.Count);

            // The 6th element is rejected — the power-of-two slot is reserved, not exposed.
            Assert.False(deque.TryAddLast(5));

            for (var i = 0; i < 5; i++)
            {
                Assert.True(deque.TryPopFirst(out var item));
                Assert.Equal(i, item);
            }

            Assert.True(deque.IsEmpty);
        }
    }
}
