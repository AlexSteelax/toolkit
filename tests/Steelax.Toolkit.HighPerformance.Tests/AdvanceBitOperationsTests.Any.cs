namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class HasAnyBits
    {
        [Theory]
        [InlineData(0b0u, false)] // no bits set
        [InlineData(0b1u, true)] // single bit set
        [InlineData(0b1111_1111_1111_1111_1111_1111_1111_1111u, true)] // all bits set
        public void WithBoundaryMasks_ReturnsCorrectly(uint mask, bool expected)
        {
            Assert.Equal(expected, AdvanceBitOperations.HasAnyBits(mask));
        }
    }
}