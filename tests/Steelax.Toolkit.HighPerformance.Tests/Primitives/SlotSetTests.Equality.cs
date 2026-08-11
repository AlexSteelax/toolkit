using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Equality
    {
        [Fact]
        public void SameMask_AreEqual()
        {
            var a = SlotSet.FromMask(0b1011);
            var b = SlotSet.FromMask(0b1011);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void DifferentMask_AreNotEqual()
        {
            var a = SlotSet.FromMask(0b1011);
            var b = SlotSet.FromMask(0b0111);

            Assert.True(a != b);
            Assert.False(a == b);
            Assert.False(a.Equals(b));
        }
    }
}