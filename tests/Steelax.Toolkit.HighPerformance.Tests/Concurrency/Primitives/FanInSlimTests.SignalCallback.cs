using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class SignalCallback
    {
        [Fact]
        public void Fire_SignalsTheSpecifiedSlot()
        {
            var source = new FanInSlim();
            var callback = source.GetSignalCallback(3);

            callback.Fire();

            var slots = source.Take();
            Assert.True(slots.IsSet(3));
        }

        [Fact]
        public void Default_IsNoOp()
        {
            var callback = default(FanInSignalCallback);

            callback.Fire(); // should not throw
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        [InlineData(33)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
        {
            var source = new FanInSlim();

            Assert.Throws<ArgumentOutOfRangeException>(() => source.GetSignalCallback(invalidIndex));
        }
    }
}