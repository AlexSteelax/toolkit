using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Remove
    {
        [Fact]
        public void SetSlot_RemovesAndReturnsOriginalTrue()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3
            var rest = set.Remove(1, out var original);

            Assert.True(original);
            Assert.Equal(0b1001u, rest.Mask); // slots 0, 3
        }

        [Fact]
        public void UnsetSlot_ReturnsSameAndOriginalFalse()
        {
            var set = SlotSet.FromMask(0b1011); // slots 0, 1, 3
            var rest = set.Remove(2, out var original);

            Assert.False(original);
            Assert.Equal(0b1011u, rest.Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.Remove(invalid, out _));
        }
    }
}