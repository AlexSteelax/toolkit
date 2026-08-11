namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class Factory
    {
        [Fact]
        public void AsNonBlocking_WrapsEnumerator()
        {
            var source = new[] { 1 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();

            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(1, adapter.GetResult());
        }
    }
}