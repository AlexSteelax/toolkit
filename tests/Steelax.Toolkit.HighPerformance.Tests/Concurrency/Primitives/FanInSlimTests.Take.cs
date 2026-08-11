using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class Take
    {
        [Fact]
        public void ReturnsAndClearsAllReadySlots()
        {
            var source = new FanInSlim();
            source.Signal(0);
            source.Signal(2);

            var slots = source.Take();

            Assert.Equal(0b101u, slots.Mask);

            // Take when empty
            Assert.False(source.Take().Any);
        }
    }
}