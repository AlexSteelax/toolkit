namespace Steelax.Toolkit.HighPerformance.Tests;

public static partial class AdvanceBitOperationsTests
{
    public sealed class HasAll
    {
        [Theory]
        [InlineData(0b0u, false)] // no bits set
        [InlineData(0b1u, false)] // single bit set
        [InlineData(0b1111_1111_1111_1111_1111_1111_1111_1110u, false)] // all but one bit set
        [InlineData(0b1111_1111_1111_1111_1111_1111_1111_1111u, true)] // all bits set
        public void WithBoundaryMasks_ReturnsCorrectly(uint mask, bool expected)
        {
            Assert.Equal(expected, AdvanceBitOperations.HasAll(mask));
        }
    }
}