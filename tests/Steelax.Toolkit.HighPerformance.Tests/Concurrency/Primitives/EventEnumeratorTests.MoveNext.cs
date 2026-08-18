namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class MoveNext
    {
        [Fact]
        public void WithSynchronousValue_StateIsReady()
        {
            var source = new[] { 1 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();

            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(1, adapter.GetResult());
        }

        [Fact]
        public void WithEmptySource_StateIsCompleted()
        {
            var source = Array.Empty<int>().ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();

            Assert.True(adapter.GetState().IsEndOfStream);
            Assert.Null(adapter.Exception);
            Assert.Throws<InvalidOperationException>(() => adapter.GetResult());
        }

        [Fact]
        public void MultipleValues_IteratesAllThenCompletes()
        {
            var values = new[] { 10, 20, 30 };
            var source = values.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            var retrieved = new List<int>();
            for (var i = 0; i < values.Length; i++)
            {
                adapter.MoveNext();
                Assert.True(adapter.GetState().IsCompletedSuccessfully);
                retrieved.Add(adapter.GetResult());
            }

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsEndOfStream);

            Assert.Equal(values, retrieved);
        }

        [Fact]
        public void SecondCallBeforeConsume_Throws()
        {
            var source = new[] { 1, 2 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();

            // State is Pending — advancing again before the iteration is consumed is a protocol violation.
            Assert.Throws<InvalidOperationException>(() => adapter.MoveNext());

            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(1, adapter.GetResult()); // did not advance to the second element
        }

        [Fact]
        public void AfterReady_StartsNextMove()
        {
            var source = new[] { 1, 2 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();
            Assert.Equal(1, adapter.GetResult());

            adapter.MoveNext();
            Assert.Equal(2, adapter.GetResult());

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsEndOfStream);
        }

        [Fact]
        public async Task WithSynchronousValue_InvokesCallback()
        {
            var callbackCount = 0;
            var source = new[] { 42 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => Interlocked.Increment(ref callbackCount);

            adapter.MoveNext();

            await WaitUntilAsync(() => callbackCount == 1, TestContext.Current.CancellationToken);
            Assert.Equal(1, callbackCount);
        }
    }
}