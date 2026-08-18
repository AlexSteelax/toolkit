using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

public static partial class SpscQueueTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void NonPositiveCapacity_Throws(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpscQueue<int>(capacity));
    }
}
