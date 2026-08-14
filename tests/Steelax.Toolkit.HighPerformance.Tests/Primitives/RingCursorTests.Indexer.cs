using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class Indexer
    {
        [Fact]
        public void Indexer_ReturnsRef_ToReservedSlot()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot());
            ring.AdvanceLast(out var offset);

            ref var slot = ref ring[offset];
            slot.Value = 42;

            Assert.Equal(42, ring[offset].Value);
        }

        [Fact]
        public void Indexer_FromEnd_AccessesWindow()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot());
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            ref var last = ref ring[^1];
            last.Value = 30;

            ref var first = ref ring[^3];
            first.Value = 10;

            Assert.True(ring.PeekLast(out var lastOffset));
            Assert.Equal(30, ring[lastOffset].Value);

            Assert.True(ring.PeekFirst(out var firstOffset));
            Assert.Equal(10, ring[firstOffset].Value);
        }

        [Fact]
        public void Indexer_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var ring = new RingCursor<int>(2, static () => 0);
            ring.AdvanceLast(out _);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = ring[^2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = ring[1]);
        }
    }
}
