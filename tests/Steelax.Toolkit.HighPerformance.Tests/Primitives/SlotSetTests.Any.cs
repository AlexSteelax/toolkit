using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Any
    {
        [Fact]
        public void WithAndWithoutBits_ReturnsCorrectly()
        {
            Assert.True(SlotSet.FromMask(1).Any);
            Assert.False(SlotSet.FromMask(0).Any);
        }
    }
}