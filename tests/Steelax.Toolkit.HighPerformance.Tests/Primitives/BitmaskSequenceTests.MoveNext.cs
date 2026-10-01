using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class BitmaskSequenceTests
{
    public sealed class MoveNext
    {
        [Fact]
        public void ReturnsValuesInStepOrder_WithinRange()
        {
            var sequence = BitmaskSequence.Create(4, 1, 17);

            Assert.Equal([1, 5, 9, 13, 17], Collect(ref sequence));
        }

        [Fact]
        public void IncludesMaxRange_WhenAligned()
        {
            var sequence = BitmaskSequence.Create(4, 2, 18, 10);

            Assert.Equal([10, 14, 18], Collect(ref sequence));
        }

        [Fact]
        public void StopsBeforeExceedingMaxRange_WhenNotAligned()
        {
            var sequence = BitmaskSequence.Create(6, 2, 30);

            Assert.Equal([2, 8, 14, 20, 26], Collect(ref sequence));
        }

        [Fact]
        public void WithDivisorOne_EnumeratesEveryInteger()
        {
            var sequence = BitmaskSequence.Create(1, 0, 4);

            Assert.Equal([0, 1, 2, 3, 4], Collect(ref sequence));
        }

        [Fact]
        public void AfterExhaustion_ReturnsFalse()
        {
            var sequence = BitmaskSequence.Create(4, 1, 10);

            Assert.Equal([1, 5, 9], Collect(ref sequence));

            Assert.False(sequence.MoveNext(out var value));
            Assert.Equal(0, value);
        }

        [Fact]
        public void EmptyRange_ReturnsFalseImmediately()
        {
            // No value congruent to 0 modulo 4 falls into [10..11]: the first candidate is 12.
            var sequence = BitmaskSequence.Create(4, 0, 11, 10);

            Assert.False(sequence.MoveNext(out var value));
            Assert.Equal(0, value);
        }

        [Fact]
        public void LargeDivisor_NearIntMax_DoesNotOverflowAndTerminates()
        {
            // Stepping by 2^30 would overflow an int after the second step; the sequence must
            // terminate instead of cycling.
            var sequence = BitmaskSequence.Create(1 << 30, 0, int.MaxValue);

            Assert.Equal([0, 1 << 30], Collect(ref sequence));
        }
    }
}