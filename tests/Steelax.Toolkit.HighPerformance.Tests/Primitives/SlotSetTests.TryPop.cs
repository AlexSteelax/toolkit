using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class TryPop
    {
        [Fact]
        public void SingleBit_ReturnsSlotAndEmptyRest()
        {
            var set = SlotSet.FromMask(4); // slot 2

            var removed = set.TryPop(out var index, out var rest);

            Assert.True(removed);
            Assert.Equal(2, index);
            Assert.False(rest.Any);
        }

        [Fact]
        public void MultipleBits_ReturnsInAscendingOrder()
        {
            var set = SlotSet.FromMask(0b10101); // slots 0, 2, 4

            Assert.True(set.TryPop(out var i0, out set));
            Assert.Equal(0, i0);

            Assert.True(set.TryPop(out var i2, out set));
            Assert.Equal(2, i2);

            Assert.True(set.TryPop(out var i4, out set));
            Assert.Equal(4, i4);

            Assert.False(set.Any);
        }

        [Fact]
        public void Empty_ReturnsFalseAndEmptyRest()
        {
            var set = SlotSet.FromMask(0);

            var removed = set.TryPop(out _, out var rest);

            Assert.False(removed);
            Assert.False(rest.Any);
        }

        [Fact]
        public void AllBits_PopsInAscendingOrder()
        {
            var set = SlotSet.FromMask(0b1111); // slots 0..3

            for (var expected = 0; expected < 4; expected++)
            {
                Assert.True(set.TryPop(out var actual, out set));
                Assert.Equal(expected, actual);
            }

            Assert.False(set.TryPop(out _, out _));
        }

        [Fact]
        public void WithStarted_PopsCyclicallyFromStartPosition()
        {
            var set = SlotSet.FromMask(0b111); // slots 0, 1, 2

            Assert.True(set.TryPop(out var first, out set, started: 2));
            Assert.Equal(2, first);

            Assert.True(set.TryPop(out var second, out set, started: 2));
            Assert.Equal(0, second);

            Assert.True(set.TryPop(out var third, out set, started: 2));
            Assert.Equal(1, third);

            Assert.False(set.Any);
        }

        [Fact]
        public void WithStarted_BelowLowestSetSlot_ReturnsNextSetSlot()
        {
            var set = SlotSet.FromMask(0b10100); // slots 2, 4

            Assert.True(set.TryPop(out var index, out var rest, started: 1));

            Assert.Equal(2, index);
            Assert.Equal(0b10000u, rest.Mask); // slot 4
        }

        [Fact]
        public void WithStarted_EqualToTopSetSlot_WrapsAround()
        {
            var set = SlotSet.FromMask(0b1000001); // slots 0, 6

            Assert.True(set.TryPop(out var top, out set, started: 6));
            Assert.Equal(6, top);

            Assert.True(set.TryPop(out var wrapped, out set, started: 6));
            Assert.Equal(0, wrapped);
        }

        [Fact]
        public void WithStarted_DefaultZero_PreservesAscendingOrder()
        {
            var set = SlotSet.FromMask(0b1010); // slots 1, 3

            Assert.True(set.TryPop(out var first, out set, started: 0));
            Assert.Equal(1, first);

            Assert.True(set.TryPop(out var second, out set, started: 0));
            Assert.Equal(3, second);

            Assert.False(set.Any);
        }

        [Theory]
        [InlineData(0b111u, 0, 0, 0b110u)]
        [InlineData(0b111u, 2, 2, 0b011u)]
        [InlineData(0b10100u, 1, 2, 0b10000u)]
        [InlineData(0b10100u, 3, 4, 0b100u)]
        [InlineData(0b1000001u, 6, 6, 1u)]
        [InlineData(1u, 1, 0, 0u)]
        [InlineData(0x8000_0000u, 0, 31, 0u)]
        [InlineData(0x8000_0001u, 31, 31, 1u)]
        public void WithVariousMasksAndStartPositions_PopsExpectedSlot(uint mask, int started, int expectedIndex, uint expectedRestMask)
        {
            var removed = SlotSet.FromMask(mask).TryPop(out var index, out var rest, started);

            Assert.True(removed);
            Assert.Equal(expectedIndex, index);
            Assert.Equal(expectedRestMask, rest.Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidStarted_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.TryPop(out _, out _, invalid));
        }
    }
}
