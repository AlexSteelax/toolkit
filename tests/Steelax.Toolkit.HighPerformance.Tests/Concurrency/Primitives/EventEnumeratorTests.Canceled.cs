using System.Threading.Channels;

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

        [Fact]
        public async Task CanceledToken_WhileWaitingForItem_StateIsCanceled()
        {
            using var cts = new CancellationTokenSource();
            var channel = Channel.CreateUnbounded<int>();
            var callbackTcs = new TaskCompletionSource();
            var source = new ChannelAsyncEnumerable(channel);
            var adapter = source.GetAsyncEnumerator(cts.Token).AsNonBlocking();
            adapter.OnReady += () => callbackTcs.TrySetResult();

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsPending);

            await cts.CancelAsync();
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(adapter.GetState().IsCanceled);
            Assert.Null(adapter.Exception);
            Assert.Throws<OperationCanceledException>(() => adapter.GetResult());
        }

        [Fact]
        public void AlreadyCanceledToken_BeforeStart_StateIsCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var channel = Channel.CreateUnbounded<int>();
            var source = new ChannelAsyncEnumerable(channel);
            var adapter = source.GetAsyncEnumerator(cts.Token).AsNonBlocking();

            adapter.MoveNext();

            Assert.True(adapter.GetState().IsCanceled);
            Assert.Null(adapter.Exception);
            Assert.Throws<OperationCanceledException>(() => adapter.GetResult());
        }

        [Fact]
        public async Task CanceledToken_AfterItemConsumed_NextMoveIsCanceled()
        {
            using var cts = new CancellationTokenSource();
            var channel = Channel.CreateUnbounded<int>();
            var callbackTcs = new TaskCompletionSource();
            var source = new ChannelAsyncEnumerable(channel);
            var adapter = source.GetAsyncEnumerator(cts.Token).AsNonBlocking();
            adapter.OnReady += () => callbackTcs.TrySetResult();

            channel.Writer.TryWrite(1);
            adapter.MoveNext();
            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(1, adapter.GetResult());

            callbackTcs = new TaskCompletionSource();
            await cts.CancelAsync();
            adapter.MoveNext();
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(adapter.GetState().IsCanceled);
            Assert.Throws<OperationCanceledException>(() => adapter.GetResult());
        }
    }
}
