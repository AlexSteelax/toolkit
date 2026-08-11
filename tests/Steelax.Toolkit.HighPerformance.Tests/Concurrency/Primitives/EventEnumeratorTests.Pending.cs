namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class Pending
    {
        [Fact]
        public void WithPendingSource_StateIsPending()
        {
            var tcs = new TaskCompletionSource<int>();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();

            Assert.True(adapter.GetState().IsPending);
            Assert.Throws<InvalidOperationException>(() => adapter.GetResult());
        }

        [Fact]
        public void WithPendingSource_CallbackNotInvokedSynchronously()
        {
            var callbackCount = 0;
            var tcs = new TaskCompletionSource<int>();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => callbackCount++;

            adapter.MoveNext();

            Assert.Equal(0, callbackCount);
        }

        [Fact]
        public async Task AfterCompletion_StateIsReady_AndCallbackInvoked()
        {
            var tcs = new TaskCompletionSource<int>();
            var callbackTcs = new TaskCompletionSource();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => callbackTcs.TrySetResult();

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsPending);

            tcs.SetResult(42);
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(42, adapter.GetResult());
        }

        [Fact]
        public async Task AfterCompletion_NextMove_ReachesEnd()
        {
            var tcs = new TaskCompletionSource<int>();
            var callbackTcs = new TaskCompletionSource();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => callbackTcs.TrySetResult();

            adapter.MoveNext();
            tcs.SetResult(1);
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(1, adapter.GetResult());

            adapter.MoveNext(); // source returns false after the single TCS
            Assert.True(adapter.GetState().IsEndOfStream);
        }
    }
}