using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Mask
    {
        [Fact]
        public void ReturnsRawBitmask()
        {
            var set = SlotSet.FromMask(0b1010);

            Assert.Equal(0b1010u, set.Mask);
        }
    }
}