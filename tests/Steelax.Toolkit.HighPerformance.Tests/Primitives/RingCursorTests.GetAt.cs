using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingCursorTests
{
    public sealed class GetAt
    {
        [Fact]
        public void GetAt_Zero_EqualsIndexerFront()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot { Value = 5 });
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            ref var viaGet = ref ring.GetAt(0);
            ref var viaIndex = ref ring[0];

            Assert.Equal(5, viaGet.Value);
            Assert.Equal(5, viaIndex.Value);
        }

        [Fact]
        public void GetAt_Negative_WrapsAroundByCapacity()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot { Value = 0 });
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            // -1 == capacity - 1 == 3; only slots 0..2 are occupied, so it throws.
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = ring.GetAt(-1));
        }

        [Fact]
        public void GetAt_GeCapacity_WrapsAround()
        {
            var ring = new RingCursor<Slot>(3, static () => new Slot());
            ring.AdvanceLast(out var a);
            ring.AdvanceLast(out var b);
            ring.AdvanceLast(out var c);

            // capacity == 3: GetAt(3) normalizes to 0, GetAt(4) to 1.
            Assert.Equal(ring[a].Value, ring.GetAt(3).Value);
            Assert.Equal(ring[b].Value, ring.GetAt(4).Value);
            Assert.Equal(ring[c].Value, ring.GetAt(5).Value);
        }

        [Fact]
        public void GetAt_FromEnd_ResolvesWithinWindow()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot { Value = 0 });
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            // ^1 == capacity - 1 == 3; slot 3 is not occupied (Count == 3), so it throws.
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = ring.GetAt(^1));
        }

        [Fact]
        public void GetAt_AllowsInPlaceMutation()
        {
            var ring = new RingCursor<Slot>(4, static () => new Slot());
            ring.AdvanceLast(out _);
            ring.AdvanceLast(out _);

            ref var slot = ref ring.GetAt(1);
            slot.Value = 42;

            Assert.Equal(42, ring[1].Value);
        }
    }
}
