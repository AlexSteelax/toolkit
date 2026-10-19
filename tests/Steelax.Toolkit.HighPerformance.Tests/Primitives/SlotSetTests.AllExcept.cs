using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class AllExcept
    {
        [Theory]
        [InlineData(0, 0b1111_1111_1111_1111_1111_1111_1111_1111u)]
        [InlineData(1, 0b1111_1111_1111_1111_1111_1111_1111_1110u)]
        [InlineData(3, 0b1111_1111_1111_1111_1111_1111_1111_1000u)]
        [InlineData(16, 0b1111_1111_1111_1111_0000_0000_0000_0000u)]
        [InlineData(31, 0b1000_0000_0000_0000_0000_0000_0000_0000u)]
        [InlineData(32, 0b0u)]
        public void SetsEverySlotExceptLowestCount(int count, uint expectedMask)
        {
            var set = SlotSet.AllExcept(count);

            Assert.Equal(expectedMask, set.Mask);
            Assert.Equal(32 - count, set.Count);
        }

        [Fact]
        public void WithLowBitsFilledIn_BecomesAll()
        {
            var set = SlotSet.AllExcept(2);
            
            Assert.True(set.TrySet(0, out set));
            Assert.True(set.TrySet(1, out set));
            Assert.True(set.All);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(32)]
        public void IsComplementOfOfBits(int count)
        {
            Assert.Equal(SlotSet.OfBits(count).Mask, ~SlotSet.AllExcept(count).Mask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(33)]
        public void WithInvalidCount_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotSet.AllExcept(invalid));
        }
    }
}
