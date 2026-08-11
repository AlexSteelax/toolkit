using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class StringRepresentation
    {
        [Fact]
        public void ReturnsMaskFollowedBySetSlots()
        {
            var set = SlotSet.Of(0, 1, 3); // mask 11

            Assert.Equal("11[0 1 3]", set.ToString());
        }

        [Fact]
        public void Empty_ReturnsZeroAndEmptyBrackets()
        {
            Assert.Equal("0[]", SlotSet.FromMask(0).ToString());
        }
    }
}