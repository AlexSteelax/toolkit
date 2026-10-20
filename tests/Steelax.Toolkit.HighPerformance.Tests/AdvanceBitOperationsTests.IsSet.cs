namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class IsBitSet
    {
        [Fact]
        public void ReturnsTrueOnlyForSetSlots()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            Assert.True(AdvanceBitOperations.IsBitSet(mask, 0));
            Assert.True(AdvanceBitOperations.IsBitSet(mask, 1));
            Assert.False(AdvanceBitOperations.IsBitSet(mask, 2));
            Assert.True(AdvanceBitOperations.IsBitSet(mask, 3));
        }

        [Theory]
        [InlineData(0b1011u, 0, true)]
        [InlineData(0b1011u, 1, true)]
        [InlineData(0b1011u, 2, false)]
        [InlineData(0b1011u, 3, true)]
        [InlineData(0x8000_0000u, 31, true)]
        [InlineData(0u, 17, false)]
        [InlineData(uint.MaxValue, 15, true)]
        public void WithVariousMasksAndIndices_ReturnsCorrectly(uint mask, int index, bool expected)
        {
            Assert.Equal(expected, AdvanceBitOperations.IsBitSet(mask, index));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.IsBitSet(1, invalid));
        }
    }
}