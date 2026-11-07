using System.Threading.Channels;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

/// <summary>
/// Unit tests for the <see cref="EventEnumerator{T}"/> class.
/// </summary>
public static partial class EventEnumeratorTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition())
            await Task.Delay(10, token);
    }

    internal static class AsyncEnumerableFactory
    {
        public static IAsyncEnumerable<int> Create(TaskCompletionSource<int>[] completionSources)
            => new AsyncEnumerableImpl(completionSources);

        private sealed class AsyncEnumerableImpl(TaskCompletionSource<int>[] completionSources) : IAsyncEnumerable<int>
        {
            public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                => new AsyncEnumeratorImpl(completionSources);
        }

        private sealed class AsyncEnumeratorImpl(TaskCompletionSource<int>[] completionSources) : IAsyncEnumerator<int>
        {
            private int _index = -1;

            public int Current { get; private set; }

            public async ValueTask<bool> MoveNextAsync()
            {
                _index++;
                if (_index >= completionSources.Length)
                    return false;

                var value = await completionSources[_index].Task;
                Current = value;
                return true;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    internal sealed class TrackingAsyncEnumerable(Action onDispose) : IAsyncEnumerable<int>
    {
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new TrackingAsyncEnumerator(onDispose);

        private sealed class TrackingAsyncEnumerator(Action onDispose) : IAsyncEnumerator<int>
        {
            public int Current => 0;

            public ValueTask<bool> MoveNextAsync() => new(false);

            public ValueTask DisposeAsync()
            {
                onDispose.Invoke();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// An async enumerable backed by a <see cref="Channel{T}"/>, where <see cref="ChannelReader{T}.WaitToReadAsync"/>
    /// observes the <see cref="CancellationToken"/> passed to <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/>.
    /// </summary>
    internal sealed class ChannelAsyncEnumerable(Channel<int> channel, Action? onDispose = null) : IAsyncEnumerable<int>
    {
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new ChannelAsyncEnumerator(channel.Reader, cancellationToken, onDispose);

        private sealed class ChannelAsyncEnumerator(ChannelReader<int> reader, CancellationToken token, Action? onDispose) : IAsyncEnumerator<int>
        {
            public int Current { get; private set; }

            public async ValueTask<bool> MoveNextAsync()
            {
                while (await reader.WaitToReadAsync(token))
                {
                    if (reader.TryRead(out var value))
                    {
                        Current = value;
                        return true;
                    }
                }

                return false;
            }

            public ValueTask DisposeAsync()
            {
                onDispose?.Invoke();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// An async enumerable whose <c>MoveNextAsync</c> is driven by an externally supplied
    /// <see cref="TaskCompletionSource{TResult}"/> and whose disposal is tracked.
    /// </summary>
    internal sealed class PendingDisposeAsyncEnumerable(TaskCompletionSource<bool> completion, Action? onDispose = null) : IAsyncEnumerable<int>
    {
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new PendingDisposeAsyncEnumerator(completion, onDispose);

        private sealed class PendingDisposeAsyncEnumerator(TaskCompletionSource<bool> completion, Action? onDispose) : IAsyncEnumerator<int>
        {
            public int Current => 0;

            public ValueTask<bool> MoveNextAsync() => new(completion.Task);

            public ValueTask DisposeAsync()
            {
                onDispose?.Invoke();
                return ValueTask.CompletedTask;
            }
        }
    }
}
