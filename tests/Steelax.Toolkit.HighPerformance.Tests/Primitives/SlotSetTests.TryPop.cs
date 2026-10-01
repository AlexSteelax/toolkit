using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class SlotSetTests
{
    public sealed class TryPop
    {
        [Fact]
        public void SingleBit_ReturnsSlotAndEmptyRest()
        {
            var set = SlotSet.FromMask(4); // slot 2

            var removed = set.TryPop(out var index, out var rest);

            Assert.True(removed);
            Assert.Equal(2, index);
            Assert.False(rest.Any);
        }

        [Fact]
        public void MultipleBits_ReturnsInAscendingOrder()
        {
            var set = SlotSet.FromMask(0b10101); // slots 0, 2, 4

            Assert.True(set.TryPop(out var i0, out set));
            Assert.Equal(0, i0);

            Assert.True(set.TryPop(out var i2, out set));
            Assert.Equal(2, i2);

            Assert.True(set.TryPop(out var i4, out set));
            Assert.Equal(4, i4);

            Assert.False(set.Any);
        }

        [Fact]
        public void Empty_ReturnsFalseAndEmptyRest()
        {
            var set = SlotSet.FromMask(0);

            var removed = set.TryPop(out _, out var rest);

            Assert.False(removed);
            Assert.False(rest.Any);
        }

        [Fact]
        public void AllBits_PopsInAscendingOrder()
        {
            var set = SlotSet.FromMask(0b1111); // slots 0..3

            for (var expected = 0; expected < 4; expected++)
            {
                Assert.True(set.TryPop(out var actual, out set));
                Assert.Equal(expected, actual);
            }

            Assert.False(set.TryPop(out _, out _));
        }
    }
}
