using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class Pop
    {
        [Fact]
        public void SingleBit_ReturnsSlotAndEmptyRest()
        {
            var set = SlotSet.FromMask(4); // slot 2
            var rest = set.Pop(out var index);

            Assert.Equal(2, index);
            Assert.False(rest.Any);
        }

        [Fact]
        public void MultipleBits_ReturnsInAscendingOrder()
        {
            var set = SlotSet.FromMask(0b10101); // slots 0, 2, 4

            set = set.Pop(out var i0);
            Assert.Equal(0, i0);

            set = set.Pop(out var i2);
            Assert.Equal(2, i2);

            set = set.Pop(out var i4);
            Assert.Equal(4, i4);

            Assert.False(set.Any);
        }

        [Fact]
        public void Empty_ReturnsNoneAndEmptyRest()
        {
            var set = SlotSet.FromMask(0);
            var rest = set.Pop(out var index);

            Assert.Equal(SlotSet.None, index);
            Assert.False(rest.Any);
        }

        [Fact]
        public void AllBits_PopsInAscendingOrder()
        {
            var set = SlotSet.FromMask(0b1111); // slots 0..3

            for (var expected = 0; expected < 4; expected++)
            {
                set = set.Pop(out var actual);
                Assert.Equal(expected, actual);
            }

            Assert.False(set.Any);
        }
    }
}