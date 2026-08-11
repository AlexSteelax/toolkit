namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class Dispose
    {
        [Fact]
        public async Task DisposeAsync_DisposesUnderlyingEnumerator()
        {
            var disposeCount = 0;
            var source = new TrackingAsyncEnumerable(() => disposeCount++);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            await adapter.DisposeAsync();

            Assert.Equal(1, disposeCount);
        }
    }
}