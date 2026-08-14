using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    /// <summary>
    /// An end-to-end scenario that exercises the full cursor lifecycle — reserve (Advance),
    /// read (Peek), mutate in place (indexer / GetAt) and shrink (Shrink) — asserting the
    /// ring state after every operation.
    /// </summary>
    public sealed class Flow
    {
        [Fact]
        public void FullLifecycle_KeepsStateConsistent()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot());

            // Initial state: empty, pre-allocated.
            Assert.Equal(4, ring.Capacity);
            Assert.Equal(0, ring.Count);
            Assert.True(ring.IsEmpty);

            // Reserve slot at the back -> offset 0, Count 1.
            Assert.True(ring.AdvanceLast(out var first));
            Assert.Equal(new Index(0), first);
            Assert.Equal(1, ring.Count);
            Assert.False(ring.IsEmpty);

            // Peek the front without changing the window.
            Assert.True(ring.PeekFirst(out var front));
            Assert.Equal(new Index(0), front);
            Assert.Equal(1, ring.Count);

            // Mutate the reserved slot in place.
            ring[first].Value = 10;
            Assert.Equal(10, ring[0].Value);
            Assert.Equal(1, ring.Count);

            // Reserve two more slots at the back -> offsets 1 and 2, Count 3.
            Assert.True(ring.AdvanceLast(out var second));
            Assert.Equal(new Index(1), second);
            Assert.True(ring.AdvanceLast(out var third));
            Assert.Equal(new Index(2), third);
            Assert.Equal(3, ring.Count);

            // Mutate through GetAt (normalization: 4 wraps to 0, 5 wraps to 1).
            ring.GetAt(4).Value = 20;
            Assert.Equal(20, ring[0].Value);
            ring.GetAt(5).Value = 30;
            Assert.Equal(30, ring[1].Value);
            Assert.Equal(3, ring.Count);

            // Peek the back -> last occupied offset.
            Assert.True(ring.PeekLast(out var back));
            Assert.Equal(new Index(2), back);
            Assert.Equal(3, ring.Count);

            // Shrink from the back -> Count 2, offsets 0..1 remain (offset 2 is gone).
            ring.ShrinkLast();
            Assert.Equal(2, ring.Count);
            Assert.True(ring.PeekLast(out var backAfterShrink));
            Assert.Equal(new Index(1), backAfterShrink);

            // Shrink from the front -> Count 1, only offset 1 remains.
            ring.ShrinkFirst();
            Assert.Equal(1, ring.Count);
            Assert.True(ring.PeekFirst(out var remaining));
            Assert.Equal(new Index(0), remaining);
            Assert.Equal(30, ring[0].Value);

            // Shrink to empty.
            ring.ShrinkFirst();
            Assert.Equal(0, ring.Count);
            Assert.True(ring.IsEmpty);
            Assert.False(ring.PeekFirst(out _));
            Assert.False(ring.PeekLast(out _));

            // Freed slots can be reserved again.
            Assert.True(ring.AdvanceLast(out var reused));
            Assert.Equal(new Index(0), reused);
            Assert.Equal(1, ring.Count);
        }
    }
}
