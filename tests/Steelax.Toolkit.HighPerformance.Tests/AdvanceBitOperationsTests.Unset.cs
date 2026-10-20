namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class UnsetBit
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
            var rest = mask;

            AdvanceBitOperations.UnsetBit(ref rest, index);

            Assert.Equal(expected, rest);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var mask = 1u;

            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.UnsetBit(ref mask, invalid));
        }
    }
}