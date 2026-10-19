using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class OfBits
    {
        [Theory]
        [InlineData(0, 0b0u)]
        [InlineData(1, 0b1u)]
        [InlineData(3, 0b111u)]
        [InlineData(16, 0b1111_1111_1111_1111u)]
        [InlineData(31, 0b0111_1111_1111_1111_1111_1111_1111_1111u)]
        [InlineData(32, 0b1111_1111_1111_1111_1111_1111_1111_1111u)]
        public void SetsLowestCountBits(int count, uint expectedMask)
        {
            var set = SlotSet.OfBits(count);

            Assert.Equal(expectedMask, set.Mask);
            Assert.Equal(count, set.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(32)]
        public void IsComplementOfAllExcept(int count)
        {
            Assert.Equal(SlotSet.AllExcept(count).Mask, ~SlotSet.OfBits(count).Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(33)]
        public void WithInvalidCount_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotSet.OfBits(invalid));
        }
    }
}
