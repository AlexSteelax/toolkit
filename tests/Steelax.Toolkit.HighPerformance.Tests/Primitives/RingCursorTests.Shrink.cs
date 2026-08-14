using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class Shrink
    {
        [Fact]
        public void ShrinkFirst_ShrinksWindowFromFront()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot());
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            ring.ShrinkFirst();

            Assert.Equal(2, ring.Count);
            Assert.True(ring.PeekFirst(out var first));
            Assert.Equal(new Index(0), first);
        }

        [Fact]
        public void ShrinkLast_ShrinksWindowFromBack()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot());
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            ring.ShrinkLast();

            Assert.Equal(2, ring.Count);
            Assert.True(ring.PeekLast(out var last));
            Assert.Equal(new Index(1), last);
        }

        [Fact]
        public void Shrink_WhenEmpty_DoesNothing()
        {
            var ring = new RingCursor<int>(2, static () => 0);

            ring.ShrinkFirst();
            ring.ShrinkLast();

            Assert.Equal(0, ring.Count);
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public void ShrinkFreesSlots_ForFurtherAdvance()
        {
            var ring = new RingCursor<Slot>(2, static () => new Slot());

            Assert.True(ring.AdvanceLast(out _));
            Assert.True(ring.AdvanceLast(out _));
            Assert.False(ring.AdvanceLast(out _));

            // Shrink from the front, then the freed slot becomes reservable again.
            ring.ShrinkFirst();
            Assert.Equal(1, ring.Count);

            Assert.True(ring.AdvanceLast(out var offset));
            Assert.Equal(new Index(1), offset);
            Assert.Equal(2, ring.Count);
        }
    }
}
