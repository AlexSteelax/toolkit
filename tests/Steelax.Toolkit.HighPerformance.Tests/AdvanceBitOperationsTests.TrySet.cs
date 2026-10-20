namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class TrySetBit
    {
        [Fact]
        public void UnsetSlot_SetsAndReturnsTrue()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            var added = AdvanceBitOperations.TrySetBit(ref mask, 2);

            Assert.True(added);
            Assert.Equal(0b1111u, mask); // slots 0, 1, 2, 3
        }

        [Fact]
        public void SetSlot_ReturnsFalseAndLeavesMask()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            var added = AdvanceBitOperations.TrySetBit(ref mask, 1);

            Assert.False(added);
            Assert.Equal(0b1011u, mask);
        }

        [Fact]
        public void EmptyMask_SetsSingleSlot()
        {
            var mask = 0u;

            var added = AdvanceBitOperations.TrySetBit(ref mask, 31);

            Assert.True(added);
            Assert.Equal(1u << 31, mask);
        }

        [Theory]
        [InlineData(0b1011u, 2, true, 0b1111u)]
        [InlineData(0b1011u, 1, false, 0b1011u)]
        [InlineData(0u, 0, true, 1u)]
        [InlineData(0u, 31, true, 0x8000_0000u)]
        [InlineData(uint.MaxValue, 7, false, uint.MaxValue)]
        [InlineData(0x8000_0000u, 0, true, 0x8000_0001u)]
        public void WithVariousMasksAndIndices_SetsAndReportsAdded(uint mask, int index, bool expectedAdded, uint expectedMask)
        {
            var rest = mask;

            var added = AdvanceBitOperations.TrySetBit(ref rest, index);

            Assert.Equal(expectedAdded, added);
            Assert.Equal(expectedMask, rest);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var mask = 1u;

            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.TrySetBit(ref mask, invalid));
        }
    }
}