using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Of
    {
        [Fact]
        public void SetsGivenSlots()
        {
            var set = SlotSet.Of(0, 1, 3);

            Assert.Equal(0b1011u, set.Mask);
            Assert.Equal(3, set.Count);
        }

        [Fact]
        public void WithNoSlots_ReturnsEmpty()
        {
            var set = SlotSet.Of();

            Assert.False(set.Any);
            Assert.Equal(0u, set.Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        [InlineData(33)]
        public void WithInvalidSlot_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotSet.Of(invalid));
        }
    }
}