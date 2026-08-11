using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class SignalValidation
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(32)]
        [InlineData(33)]
        public void WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
        {
            var source = new FanInSlim();

            Assert.Throws<ArgumentOutOfRangeException>(() => source.Signal(invalidIndex));
        }
    }
}