using System.Numerics;

namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class OfFirstBits
    {
        [Theory]
        [InlineData(0, 0b0u)]
        [InlineData(1, 0b1u)]
        [InlineData(3, 0b111u)]
        [InlineData(16, 0b1111_1111_1111_1111u)]
        [InlineData(31, 0b0111_1111_1111_1111_1111_1111_1111_1111u)]
        [InlineData(32, 0b1111_1111_1111_1111_1111_1111_1111_1111u)]
        public void SetsFirstCountBits(int count, uint expectedMask)
        {
            var mask = AdvanceBitOperations.OfFirstBits(count);

            Assert.Equal(expectedMask, mask);
            Assert.Equal(count, BitOperations.PopCount(mask));
        }

        [Fact]
        public void WithLastBitsFilledIn_BecomesAll()
        {
            var mask = AdvanceBitOperations.OfFirstBits(30);

            Assert.True(AdvanceBitOperations.TrySet(mask, 30, out mask));
            Assert.True(AdvanceBitOperations.TrySet(mask, 31, out mask));
            Assert.True(AdvanceBitOperations.HasAll(mask));
        }

        [Theory]
        [InlineData(0b0u)]
        [InlineData(0b1u)]
        [InlineData(0b1010_1010_1010_1010u)]
        [InlineData(uint.MaxValue)]
        public void OrWithExternalMask_PreservesOrAbsorbs(uint externMask)
        {
            // OfFirstBits(0) is the empty mask: the neutral element for OR, so the external
            // mask must survive unchanged.
            var fromEmpty = AdvanceBitOperations.OfFirstBits(0) | externMask;
            Assert.Equal(externMask, fromEmpty);

            // OfFirstBits(32) is the full mask: the absorbing element for OR.
            var fromFull = AdvanceBitOperations.OfFirstBits(32) | externMask;
            Assert.True(AdvanceBitOperations.HasAll(fromFull));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(32)]
        public void IsComplementOfOfLastBits(int count)
        {
            Assert.Equal(AdvanceBitOperations.OfLastBits(count), ~AdvanceBitOperations.OfFirstBits(32 - count));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(33)]
        public void WithInvalidCount_ThrowsArgumentOutOfRangeException(int invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AdvanceBitOperations.OfFirstBits(invalid));
        }
    }
}