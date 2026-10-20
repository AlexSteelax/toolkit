namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class Unset
    {
        [Theory]
        [InlineData(0b1011u, 1, 0b1001u)]
        [InlineData(0b1011u, 2, 0b1011u)] // idempotent: already unset
        [InlineData(1u, 0, 0u)]
        [InlineData(0x8000_0000u, 31, 0u)]
        [InlineData(uint.MaxValue, 7, 0xFFFF_FF7Fu)]
        [InlineData(uint.MaxValue, 31, 0x7FFF_FFFFu)]
        public void WithVariousMasksAndIndices_UnsetsSlot(uint mask, int index, uint expected)
        {
            Assert.Equal(expected, AdvanceBitOperations.Unset(mask, index));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.Unset(1, invalid));
        }
    }
}