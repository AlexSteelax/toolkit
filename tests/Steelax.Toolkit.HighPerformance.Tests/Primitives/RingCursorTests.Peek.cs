using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class Peek
    {
        [Fact]
        public void PeekFirst_ReturnsFront_WithoutChangingWindow()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot { Value = 1 });
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            Assert.True(ring.PeekFirst(out var first));
            Assert.Equal(new Index(0), first);
            Assert.Equal(2, ring.Count);

            // Peek does not reserve a slot: Count is unchanged.
            Assert.True(ring.PeekFirst(out var again));
            Assert.Equal(new Index(0), again);
            Assert.Equal(2, ring.Count);
        }

        [Fact]
        public void PeekLast_ReturnsBack_WithoutChangingWindow()
        {
            var ring = new RingCursor<int>(4, static () => 0);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            Assert.True(ring.PeekLast(out var last));
            Assert.Equal(new Index(2), last);
            Assert.Equal(3, ring.Count);
        }

        [Fact]
        public void Peek_WhenEmpty_ReturnsFalse()
        {
            var ring = new RingCursor<int>(2, static () => 0);

            Assert.False(ring.PeekFirst(out _));
            Assert.False(ring.PeekLast(out _));
        }
    }
}
