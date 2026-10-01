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
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.TryRemove(invalid, out _));
        }
    }
}