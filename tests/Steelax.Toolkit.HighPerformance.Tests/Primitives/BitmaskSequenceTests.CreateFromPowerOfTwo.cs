using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class BitmaskSequenceTests
{
    public sealed class CreateFromPowerOfTwo
    {
        [Fact]
        public void FromZeroMinRange_StartsAtRemainder()
        {
            var sequence = BitmaskSequence.CreateFromPowerOfTwo(2, 1, 20);

            Assert.Equal([1, 5, 9, 13, 17], Collect(ref sequence));
        }

        [Fact]
        public void WithMinRange_AlignsDownByMask()
        {
            var sequence = BitmaskSequence.CreateFromPowerOfTwo(2, 0, 20, 10);

            Assert.Equal([12, 16, 20], Collect(ref sequence));
        }

        [Fact]
        public void WithMinRangeInsideGridCell_StartsAtRemainderInsideRange()
        {
            var sequence = BitmaskSequence.CreateFromPowerOfTwo(2, 3, 20, 10);

            Assert.Equal([11, 15, 19], Collect(ref sequence));
        }

        [Fact]
        public void WithZeroExponent_StepsByOne()
        {
            var sequence = BitmaskSequence.CreateFromPowerOfTwo(0, 0, 5);

            Assert.Equal([0, 1, 2, 3, 4, 5], Collect(ref sequence));
        }

        [Fact]
        public void MatchesArbitraryCreate()
        {
            var powerOfTwoSeq = BitmaskSequence.CreateFromPowerOfTwo(3, 5, 100, 4);
            var arbitrarySeq = BitmaskSequence.Create(8, 5, 100, 4);

            Assert.Equal(Collect(ref powerOfTwoSeq), Collect(ref arbitrarySeq));
        }

        [Fact]
        public void NegativeExponent_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.CreateFromPowerOfTwo(-1, 0, 10));
        }

        [Fact]
        public void ExponentThatOverflowsInt_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.CreateFromPowerOfTwo(31, 0, 10));
        }

        [Fact]
        public void NegativeRemainder_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.CreateFromPowerOfTwo(2, -1, 10));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public void RemainderNotLessThanDivisor_ThrowsArgumentOutOfRangeException(int remainder)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.CreateFromPowerOfTwo(1, remainder, 10));
        }
    }
}