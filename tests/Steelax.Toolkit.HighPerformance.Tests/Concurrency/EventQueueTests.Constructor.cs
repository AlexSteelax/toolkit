using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class EventQueueTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void NonPositiveCapacity_Throws(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new EventQueue<int>(capacity));
    }
}
