namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class State
    {
        [Fact]
        public void Result_BeforeMoveNext_Throws()
        {
            var source = new[] { 1 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            Assert.Throws<InvalidOperationException>(() => adapter.GetResult());
        }

        [Fact]
        public void ReadingMultipleTimes_IsStable()
        {
            var source = new[] { 42 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();

            var first = adapter.GetState();
            var second = adapter.GetState();
            var third = adapter.GetState();

            Assert.True(first.IsCompletedSuccessfully);
            Assert.Equal(first, second);
            Assert.Equal(second, third);
            Assert.Equal(42, adapter.GetResult());
        }
    }
}