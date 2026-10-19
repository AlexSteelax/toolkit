using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class All
    {
        [Fact]
        public void FullMask_ReturnsTrue()
        {
            Assert.True(SlotSet.FromMask(uint.MaxValue).All);
        }

        [Fact]
        public void EmptyMask_ReturnsFalse()
        {
            Assert.False(SlotSet.FromMask(0).All);
        }

        [Fact]
        public void SingleBit_ReturnsFalse()
        {
            Assert.False(SlotSet.FromMask(1u << 31).All);
        }

        [Fact]
        public void AllButOneBit_ReturnsFalse()
        {
            Assert.False(SlotSet.FromMask(uint.MaxValue - 1).All);
        }
    }
}
