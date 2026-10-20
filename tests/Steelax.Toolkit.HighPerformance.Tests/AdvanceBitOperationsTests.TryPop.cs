namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class TryPop
    {
        [Fact]
        public void SingleBit_ReturnsSlotAndClearsMask()
        {
            var mask = 4u; // slot 2

            var removed = AdvanceBitOperations.TryPop(ref mask, out var index);

            Assert.True(removed);
            Assert.Equal(2, index);
            Assert.Equal(0u, mask);
        }

        [Fact]
        public void MultipleBits_ReturnsInAscendingOrder()
        {
            var mask = 0b10101u; // slots 0, 2, 4

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var i0));
            Assert.Equal(0, i0);

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var i2));
            Assert.Equal(2, i2);

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var i4));
            Assert.Equal(4, i4);

            Assert.False(AdvanceBitOperations.HasAny(mask));
        }

        [Fact]
        public void Empty_ReturnsFalseAndLeavesMask()
        {
            var mask = 0u;

            var removed = AdvanceBitOperations.TryPop(ref mask, out _);

            Assert.False(removed);
            Assert.Equal(0u, mask);
        }

        [Fact]
        public void AllBits_PopsInAscendingOrder()
        {
            var mask = 0b1111u; // slots 0..3

            for (var expected = 0; expected < 4; expected++)
            {
                Assert.True(AdvanceBitOperations.TryPop(ref mask, out var actual));
                Assert.Equal(expected, actual);
            }

            Assert.False(AdvanceBitOperations.TryPop(ref mask, out _));
        }

        [Fact]
        public void WithOffset_PopsCyclicallyFromOffset()
        {
            var mask = 0b111u; // slots 0, 1, 2

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var first, 2));
            Assert.Equal(2, first);

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var second, 2));
            Assert.Equal(0, second);

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var third, 2));
            Assert.Equal(1, third);

            Assert.False(AdvanceBitOperations.HasAny(mask));
        }

        [Fact]
        public void WithOffset_BelowLowestSetSlot_ReturnsNextSetSlot()
        {
            var mask = 0b10100u; // slots 2, 4

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var index, 1));

            Assert.Equal(2, index);
            Assert.Equal(0b10000u, mask); // slot 4
        }

        [Fact]
        public void WithOffset_EqualToTopSetSlot_WrapsAround()
        {
            var mask = 0b1000001u; // slots 0, 6

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var top, 6));
            Assert.Equal(6, top);

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var wrapped, 6));
            Assert.Equal(0, wrapped);
        }

        [Fact]
        public void WithOffset_Zero_PreservesAscendingOrder()
        {
            var mask = 0b1010u; // slots 1, 3

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var first, 0));
            Assert.Equal(1, first);

            Assert.True(AdvanceBitOperations.TryPop(ref mask, out var second, 0));
            Assert.Equal(3, second);

            Assert.False(AdvanceBitOperations.HasAny(mask));
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
        public void WithVariousMasksAndOffsets_PopsExpectedSlot(uint mask, int offset, int expectedIndex, uint expectedRestMask)
        {
            var rest = mask;

            var removed = AdvanceBitOperations.TryPop(ref rest, out var index, offset);

            Assert.True(removed);
            Assert.Equal(expectedIndex, index);
            Assert.Equal(expectedRestMask, rest);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidOffset_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var mask = 1u;

            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.TryPop(ref mask, out _, invalid));
        }
    }
}