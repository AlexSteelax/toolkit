using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Count
    {
        [Fact]
        public void ReturnsNumberOfSetBits()
        {
            Assert.Equal(0, SlotSet.FromMask(0).Count);
            Assert.Equal(1, SlotSet.FromMask(1).Count);
            Assert.Equal(3, SlotSet.FromMask(0b1011).Count);
            Assert.Equal(32, SlotSet.FromMask(uint.MaxValue).Count);
        }
    }
}