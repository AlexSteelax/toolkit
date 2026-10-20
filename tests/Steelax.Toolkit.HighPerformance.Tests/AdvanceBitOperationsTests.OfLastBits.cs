using System.Numerics;

namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class OfLastBits
    {
        [Theory]
        [InlineData(0, 0b0u)]
        [InlineData(1, 0b1000_0000_0000_0000_0000_0000_0000_0000u)]
        [InlineData(3, 0b1110_0000_0000_0000_0000_0000_0000_0000u)]
        [InlineData(16, 0b1111_1111_1111_1111_0000_0000_0000_0000u)]
        [InlineData(31, 0b1111_1111_1111_1111_1111_1111_1111_1110u)]
        [InlineData(32, 0b1111_1111_1111_1111_1111_1111_1111_1111u)]
        public void SetsLastCountBits(int count, uint expectedMask)
        {
            var mask = AdvanceBitOperations.OfLastBits(count);

            Assert.Equal(expectedMask, mask);
            Assert.Equal(count, BitOperations.PopCount(mask));
        }

        [Theory]
        [InlineData(0b0u)]
        [InlineData(0b1u)]
        [InlineData(0b1011u)]
        [InlineData(0b1010_1010_1010_1010u)]
        [InlineData(uint.MaxValue)]
        public void OrWithExternalMask_PreservesOrAbsorbs(uint externMask)
        {
            // OfLastBits(0) is the empty mask: the neutral element for OR, so the external
            // mask must survive unchanged.
            var fromEmpty = AdvanceBitOperations.OfLastBits(0) | externMask;
            Assert.Equal(externMask, fromEmpty);

            // OfLastBits(32) is the full mask: the absorbing element for OR.
            var fromFull = AdvanceBitOperations.OfLastBits(32) | externMask;
            Assert.True(AdvanceBitOperations.HasAll(fromFull));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(32)]
        public void IsComplementOfOfFirstBits(int count)
        {
            Assert.Equal(AdvanceBitOperations.OfFirstBits(count), ~AdvanceBitOperations.OfLastBits(32 - count));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(33)]
        public void WithInvalidCount_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.OfLastBits(invalid));
        }
    }
}