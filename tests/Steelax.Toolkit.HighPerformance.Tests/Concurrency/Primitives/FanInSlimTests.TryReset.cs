using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class TryReset
    {
        [Fact]
        public void SetSlot_ResetsAndReturnsTrue()
        {
            var source = new FanInSlim();
            source.Signal(2);

            Assert.True(source.TryReset(2));
            Assert.False(source.TryReset(2)); // already reset
        }

        [Fact]
        public void UnsetSlot_ReturnsFalse_AndLeavesOthers()
        {
            var source = new FanInSlim();
            source.Signal(0);

            Assert.False(source.TryReset(2));
            Assert.True(source.Take().IsSet(0)); // Slot 0 still present
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        [InlineData(33)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
        {
            var source = new FanInSlim();

            Assert.Throws<ArgumentOutOfRangeException>(() => source.TryReset(invalidIndex));
        }
    }
}