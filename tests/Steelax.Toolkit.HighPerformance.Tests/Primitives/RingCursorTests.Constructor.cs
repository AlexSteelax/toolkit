using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void InvalidCapacity_ThrowsArgumentOutOfRangeException(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingCursor<int>(capacity, static () => 0));

        [Fact]
        public void NullFactory_ThrowsArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => new RingCursor<int>(4, null!));

        [Fact]
        public void ValidCapacity_InitializesEmptyRing()
        {
            var ring = new RingCursor<int>(4, static () => 0);

            Assert.Equal(4, ring.Capacity);
            Assert.Equal(0, ring.Count);
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public void Factory_PreAllocatesAllSlots()
        {
            var calls = 0;
            var ring = new RingCursor<Slot>(8, () => new Slot { Value = ++calls });

            // The factory is invoked once per allocated slot (rounded up to a power of two).
            Assert.Equal(8, calls);

            // Every slot is created up front; the ring starts empty but the elements are ready.
            Assert.Equal(0, ring.Count);
            Assert.True(ring.IsEmpty);
        }
    }
}
