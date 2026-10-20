using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Operators
    {
        [Theory]
        [InlineData(0b1011u, 0b0110u, 0b1111u)]
        [InlineData(0b1011u, 0b0u, 0b1011u)] // empty is the neutral element of |
        [InlineData(0b1011u, 0b1111_1111_1111_1111_1111_1111_1111_1111u, 0b1111_1111_1111_1111_1111_1111_1111_1111u)] // full is the absorbing element of |
        public void Or_UnionsMasks(uint a, uint b, uint expected)
        {
            var result = SlotSet.FromMask(a) | SlotSet.FromMask(b);

            Assert.Equal(expected, result.Mask);
        }

        [Theory]
        [InlineData(0b1011u, 0b0110u, 0b0010u)]
        [InlineData(0b1011u, 0b0u, 0b0u)] // empty is the absorbing element of &
        [InlineData(0b1011u, 0b1111_1111_1111_1111_1111_1111_1111_1111u, 0b1011u)] // full is the neutral element of &
        public void And_IntersectsMasks(uint a, uint b, uint expected)
        {
            var result = SlotSet.FromMask(a) & SlotSet.FromMask(b);

            Assert.Equal(expected, result.Mask);
        }

        [Theory]
        [InlineData(0b1011u, 0b0110u, 0b1101u)]
        [InlineData(0b1011u, 0b0u, 0b1011u)] // empty is the neutral element of ^
        [InlineData(0b1011u, 0b1011u, 0b0u)] // a ^ a == empty
        public void Xor_SymmetricDifference(uint a, uint b, uint expected)
        {
            var result = SlotSet.FromMask(a) ^ SlotSet.FromMask(b);

            Assert.Equal(expected, result.Mask);
        }

        [Theory]
        [InlineData(0b1011u, 0b1111_1111_1111_1111_1111_1111_1111_0100u)]
        [InlineData(0b0u, 0b1111_1111_1111_1111_1111_1111_1111_1111u)] // ~empty == full
        [InlineData(0b1111_1111_1111_1111_1111_1111_1111_1111u, 0b0u)] // ~full == empty
        public void Invert_InvertsMask(uint source, uint expected)
        {
            var result = ~SlotSet.FromMask(source);

            Assert.Equal(expected, result.Mask);
        }

        [Fact]
        public void Invert_OfInvert_ReturnsOriginal()
        {
            var set = SlotSet.Of(0, 1, 3);

            Assert.Equal(set, ~~set);
        }

        [Fact]
        public void Or_ChainsWithTrySet()
        {
            var set = SlotSet.Of(0, 2) | SlotSet.Of(1, 3);

            Assert.Equal(0b1111u, set.Mask);
        }
    }
}
