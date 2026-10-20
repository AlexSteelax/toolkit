namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class TryUnset
    {
        [Fact]
        public void SetSlot_RemovesAndReturnsTrue()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            var removed = AdvanceBitOperations.TryUnset(mask, 1, out var rest);

            Assert.True(removed);
            Assert.Equal(0b1001u, rest); // slots 0, 3
        }

        [Fact]
        public void UnsetSlot_ReturnsFalseAndUnchangedRest()
        {
            var mask = 0b1011u; // slots 0, 1, 3

            var removed = AdvanceBitOperations.TryUnset(mask, 2, out var rest);

            Assert.False(removed);
            Assert.Equal(0b1011u, rest);
        }

        [Theory]
        [InlineData(0b1011u, 1, true, 0b1001u)]
        [InlineData(0b1011u, 2, false, 0b1011u)]
        [InlineData(1u, 0, true, 0u)]
        [InlineData(0x8000_0000u, 31, true, 0u)]
        [InlineData(uint.MaxValue, 7, true, 0xFFFF_FF7Fu)]
        [InlineData(uint.MaxValue, 31, true, 0x7FFF_FFFFu)]
        public void WithVariousMasksAndIndices_RemovesAndReportsRemoved(uint mask, int index, bool expectedRemoved, uint expectedMask)
        {
            var removed = AdvanceBitOperations.TryUnset(mask, index, out var rest);

            Assert.Equal(expectedRemoved, removed);
            Assert.Equal(expectedMask, rest);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.TryUnset(1, invalid, out _));
        }
    }
}