using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class TrySet
    {
        [Fact]
        public void UnsetSlot_SetsAndReturnsTrue()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3

            var added = set.TrySet(2, out var rest);

            Assert.True(added);
            Assert.Equal(0b1111u, rest.Mask); // slots 0, 1, 2, 3
        }

        [Fact]
        public void SetSlot_ReturnsFalseAndUnchangedRest()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3

            var added = set.TrySet(1, out var rest);

            Assert.False(added);
            Assert.Equal(0b1011u, rest.Mask);
        }

        [Fact]
        public void EmptySet_SetsSingleSlot()
        {
            var set = SlotSet.FromMask(0);

            var added = set.TrySet(31, out var rest);

            Assert.True(added);
            Assert.Equal(1u << 31, rest.Mask);
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
            var added = SlotSet.FromMask(mask).TrySet(index, out var rest);

            Assert.Equal(expectedAdded, added);
            Assert.Equal(expectedMask, rest.Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.TrySet(invalid, out _));
        }
    }
}
