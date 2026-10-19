using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class TryRemove
    {
        [Fact]
        public void SetSlot_RemovesAndReturnsTrue()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3

            var removed = set.TryRemove(1, out var rest);

            Assert.True(removed);
            Assert.Equal(0b1001u, rest.Mask); // slots 0, 3
        }

        [Fact]
        public void UnsetSlot_ReturnsFalseAndUnchangedRest()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3

            var removed = set.TryRemove(2, out var rest);

            Assert.False(removed);
            Assert.Equal(0b1011u, rest.Mask);
        }

        [Theory]
        [InlineData(0b1011u, 1, true, 0b1001u)]
        [InlineData(0b1011u, 2, false, 0b1011u)]
        [InlineData(1u, 0, true, 0u)]
        [InlineData(0x8000_0000u, 31, true, 0u)]
        [InlineData(uint.MaxValue, 7, true, 0xFFFF_FF7Fu)]
        [InlineData(uint.MaxValue, 32 - 1, true, 0x7FFF_FFFFu)]
        public void WithVariousMasksAndIndices_RemovesAndReportsRemoved(uint mask, int index, bool expectedRemoved, uint expectedMask)
        {
            var removed = SlotSet.FromMask(mask).TryRemove(index, out var rest);

            Assert.Equal(expectedRemoved, removed);
            Assert.Equal(expectedMask, rest.Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.TryRemove(invalid, out _));
        }
    }
}