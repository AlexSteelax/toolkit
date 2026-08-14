using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class Advance
    {
        [Fact]
        public void AdvanceLast_GrowsWindowFromBack()
        {
            var ring = new RingCursor<int>(4, static () => 0);

            Assert.True(ring.AdvanceLast(out var first));
            Assert.Equal(new Index(0), first);
            Assert.Equal(1, ring.Count);

            Assert.True(ring.AdvanceLast(out var second));
            Assert.Equal(new Index(1), second);
            Assert.Equal(2, ring.Count);
        }

        [Fact]
        public void AdvanceFirst_GrowsWindowFromFront()
        {
            var ring = new RingCursor<int>(4, static () => 0);

            Assert.True(ring.AdvanceLast(out _));
            Assert.True(ring.AdvanceLast(out _));

            Assert.True(ring.AdvanceFirst(out var front));
            Assert.Equal(new Index(0), front);
            Assert.Equal(3, ring.Count);
        }

        [Fact]
        public void Advance_WhenEmpty_BothVariantsReserveSameFirstSlot()
        {
            // On an empty ring both variants reserve the same first slot (offset 0).
            var fromFront = new RingCursor<int>(4, static () => 42);
            var fromBack = new RingCursor<int>(4, static () => 42);

            Assert.True(fromFront.AdvanceFirst(out var frontOffset));
            Assert.True(fromBack.AdvanceLast(out var backOffset));

            Assert.Equal(new Index(0), frontOffset);
            Assert.Equal(new Index(0), backOffset);
        }

        [Fact]
        public void Advance_WhenFull_ReturnsFalse()
        {
            var ring = new RingCursor<int>(2, static () => 0);

            Assert.True(ring.AdvanceLast(out _));
            Assert.True(ring.AdvanceLast(out _));
            Assert.False(ring.AdvanceLast(out _));
            Assert.False(ring.AdvanceFirst(out _));
            Assert.Equal(2, ring.Count);
        }
    }
}
