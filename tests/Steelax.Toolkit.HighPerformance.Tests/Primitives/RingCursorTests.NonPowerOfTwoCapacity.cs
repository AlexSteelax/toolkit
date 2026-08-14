using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class NonPowerOfTwoCapacity
    {
        [Fact]
        public void Capacity5_IsStrictBound_WhileSlotsRoundUpToPowerOfTwo()
        {
            var calls = 0;
            var ring = new RingCursor<Slot>(5, () => new Slot { Value = ++calls });

            // Capacity is the requested bound; the internal buffer rounds up to 8 slots.
            Assert.Equal(5, ring.Capacity);
            Assert.Equal(8, calls);

            // Only 5 slots are reservable.
            for (var i = 0; i < 5; i++)
                Assert.True(ring.AdvanceLast(out _));
            Assert.False(ring.AdvanceLast(out _));
            Assert.Equal(5, ring.Count);
        }
    }
}
