namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class Canceled
    {
        [Fact]
        public async Task CanceledSource_StateIsCanceled()
        {
            using var cts = new CancellationTokenSource();
            var tcs = new TaskCompletionSource<int>();
            var callbackTcs = new TaskCompletionSource();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => callbackTcs.TrySetResult();

            adapter.MoveNext();
            tcs.SetCanceled(cts.Token);
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(adapter.GetState().IsCanceled);
            Assert.Null(adapter.Exception);
            Assert.Throws<OperationCanceledException>(() => adapter.GetResult());
        }
    }
}