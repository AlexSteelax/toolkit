namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class Set
    {
        [Theory]
        [InlineData(0b1011u, 2, 0b1111u)]
        [InlineData(0b1011u, 1, 0b1011u)] // idempotent: already set
        [InlineData(0u, 0, 1u)]
        [InlineData(0u, 31, 0x8000_0000u)]
        [InlineData(0x8000_0000u, 0, 0x8000_0001u)]
        [InlineData(uint.MaxValue, 7, uint.MaxValue)] // idempotent: already set
        public void WithVariousMasksAndIndices_SetsSlot(uint mask, int index, uint expected)
        {
            AdvanceBitOperations.Set(mask, index, out var rest);

            Assert.Equal(expected, rest);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.Set(1, invalid, out _));
        }
    }
}