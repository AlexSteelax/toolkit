namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class Faulted
    {
        [Fact]
        public async Task FaultedSource_StateIsFaulted_ExceptionCaptured()
        {
            var ex = new InvalidOperationException("Test error");
            var tcs = new TaskCompletionSource<int>();
            var callbackTcs = new TaskCompletionSource();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => callbackTcs.TrySetResult();

            adapter.MoveNext();
            tcs.SetException(ex);
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(adapter.GetState().IsFaulted);
            Assert.Same(ex, adapter.Exception);
            Assert.Same(ex, Assert.Throws<InvalidOperationException>(() => adapter.GetResult()));
        }
    }
}