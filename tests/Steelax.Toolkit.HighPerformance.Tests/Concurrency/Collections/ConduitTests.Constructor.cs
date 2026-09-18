using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

public static partial class ConduitTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void NonPositiveCapacity_Throws(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new Conduit<int>(capacity));

        [Fact]
        public async Task DefaultBehavior_CreatesBareConduit()
        {
            var conduit = new Conduit<int>(4);

            // No awaitable signal → waiting reports live status without blocking.
            Assert.True(await conduit.WaitToReadAsync());
            Assert.True(await conduit.WaitToWriteAsync());
        }

        [Fact]
        public async Task AwaitableReader_CreatesSignal()
        {
            var conduit = new Conduit<int>(4, ConduitBehavior.AwaitableReader);

            var wait = conduit.WaitToReadAsync();

            Assert.False(wait.IsCompleted);

            Assert.True(conduit.TryWrite(1));
            Assert.True(await wait);
        }
    }
}
