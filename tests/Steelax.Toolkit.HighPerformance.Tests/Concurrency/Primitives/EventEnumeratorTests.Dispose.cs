using System.Threading.Channels;

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

        [Fact]
        public async Task DisposeAsync_WithPendingOperation_AwaitsCompletionBeforeDisposing()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disposeCount = 0;
            var source = new PendingDisposeAsyncEnumerable(completion, () => disposeCount++);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsPending);

            // DisposeAsync waits for an in-flight move to settle before releasing the underlying
            // enumerator; run it on a separate thread so the move can be resolved concurrently.
            var disposeTask = Task.Run(() => adapter.DisposeAsync().AsTask(), TestContext.Current.CancellationToken);
            Assert.False(disposeTask.IsCompleted);
            Assert.Equal(0, disposeCount);

            completion.SetResult(true);
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(1, disposeCount);
        }

        [Fact]
        public async Task DisposeAsync_WithPendingCanceledOperation_DoesNotThrow_AndDisposesUnderlying()
        {
            using var cts = new CancellationTokenSource();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disposeCount = 0;
            var source = new PendingDisposeAsyncEnumerable(completion, () => disposeCount++);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsPending);

            completion.SetCanceled(cts.Token);
            await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(1, disposeCount);
        }

        [Fact]
        public async Task DisposeAsync_WithPendingFaultedOperation_DoesNotThrow_AndDisposesUnderlying()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disposeCount = 0;
            var source = new PendingDisposeAsyncEnumerable(completion, () => disposeCount++);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsPending);

            completion.SetException(new InvalidOperationException("Test error"));
            await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(1, disposeCount);
        }

        [Fact]
        public async Task DisposeAsync_AfterItemsConsumed_DisposesUnderlying()
        {
            var disposeCount = 0;
            var channel = Channel.CreateUnbounded<int>();
            var source = new ChannelAsyncEnumerable(channel, () => disposeCount++);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            channel.Writer.TryWrite(1);
            channel.Writer.TryWrite(2);

            adapter.MoveNext();
            Assert.Equal(1, adapter.GetResult());

            adapter.MoveNext();
            Assert.Equal(2, adapter.GetResult());

            await adapter.DisposeAsync();

            Assert.Equal(1, disposeCount);
        }

        [Fact]
        public async Task DisposeAsync_CanBeCalledAfterDispose()
        {
            var disposeCount = 0;
            var source = new TrackingAsyncEnumerable(() => disposeCount++);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();

            await adapter.DisposeAsync();
            await adapter.DisposeAsync();

            Assert.Equal(2, disposeCount);
        }
    }
}
