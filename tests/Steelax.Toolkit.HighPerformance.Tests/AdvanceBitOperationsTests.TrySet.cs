namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class TrySet
    {
        [Fact]
        public void UnsetSlot_SetsAndReturnsTrue()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            var added = AdvanceBitOperations.TrySet(mask, 2, out var rest);

            Assert.True(added);
            Assert.Equal(0b1111u, rest); // slots 0, 1, 2, 3
        }

        [Fact]
        public void SetSlot_ReturnsFalseAndUnchangedRest()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            var added = AdvanceBitOperations.TrySet(mask, 1, out var rest);

            Assert.False(added);
            Assert.Equal(0b1011u, rest);
        }

        [Fact]
        public void EmptyMask_SetsSingleSlot()
        {
            var added = AdvanceBitOperations.TrySet(0, 31, out var rest);

            Assert.True(added);
            Assert.Equal(1u << 31, rest);
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
            var added = AdvanceBitOperations.TrySet(mask, index, out var rest);

            Assert.Equal(expectedAdded, added);
            Assert.Equal(expectedMask, rest);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.TrySet(1, invalid, out _));
        }
    }
}