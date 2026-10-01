using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class BitmaskSequenceTests
{
    public sealed class Create
    {
        [Fact]
        public void FromZeroMinRange_StartsAtRemainder()
        {
            var sequence = BitmaskSequence.Create(4, 1, 20);

            Assert.Equal([1, 5, 9, 13, 17], Collect(ref sequence));
        }

        [Fact]
        public void WithRemainderGreaterThanZero_ProducesNonMultiples()
        {
            var sequence = BitmaskSequence.Create(5, 2, 30);

            Assert.Equal([2, 7, 12, 17, 22, 27], Collect(ref sequence));
        }

        [Fact]
        public void WithMinRange_AlignsUpToTheDivisorGrid()
        {
            var sequence = BitmaskSequence.Create(3, 0, 20, 10);

            Assert.Equal([12, 15, 18], Collect(ref sequence));
        }

        [Fact]
        public void WithMinRangeInsideGridCell_StartsAtRemainderInsideRange()
        {
            var sequence = BitmaskSequence.Create(4, 3, 24, 10);

            Assert.Equal([11, 15, 19, 23], Collect(ref sequence));
        }

        [Fact]
        public void WithZeroRemainder_ProducesMultiplesOfDivisor()
        {
            var sequence = BitmaskSequence.Create(7, 0, 30, 10);

            Assert.Equal([14, 21, 28], Collect(ref sequence));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void NonPositiveDivisor_ThrowsArgumentOutOfRangeException(int divisor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.Create(divisor, 0, 10));
        }

        [Fact]
        public void NegativeRemainder_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.Create(4, -1, 10));
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        public void RemainderNotLessThanDivisor_ThrowsArgumentOutOfRangeException(int remainder)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BitmaskSequence.Create(4, remainder, 10));
        }
    }
}