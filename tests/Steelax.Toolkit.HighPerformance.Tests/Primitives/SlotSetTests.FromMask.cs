using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class FromMask
    {
        [Fact]
        public void PreservesMask()
        {
            var set = SlotSet.FromMask(0b1011);

            Assert.Equal(0b1011u, set.Mask);
        }
    }
}