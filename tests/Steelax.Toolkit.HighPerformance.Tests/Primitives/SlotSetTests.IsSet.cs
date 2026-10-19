using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class IsSet
    {
        [Fact]
        public void ReturnsTrueOnlyForSetSlots()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3

            Assert.True(set.IsSet(0));
            Assert.True(set.IsSet(1));
            Assert.False(set.IsSet(2));
            Assert.True(set.IsSet(3));
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
            Assert.Equal(expected, SlotSet.FromMask(mask).IsSet(index));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.IsSet(invalid));
        }
    }
}