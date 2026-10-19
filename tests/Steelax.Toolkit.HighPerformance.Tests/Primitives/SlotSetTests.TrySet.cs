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
        [InlineData(-1)]
        [InlineData(32)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalid)
        {
            var set = SlotSet.FromMask(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => set.TrySet(invalid, out _));
        }
    }
}
