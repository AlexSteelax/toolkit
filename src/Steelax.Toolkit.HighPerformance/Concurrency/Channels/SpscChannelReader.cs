using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// A bounded, lock-free SPSC queue for subscription-based writing: the writer observes readiness via
/// <see cref="OnWriteReady"/> (raised when a slot of a full buffer is freed), while the reader waits via
/// <see cref="WaitToReadAsync"/>.
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <param name="capacity">The maximum number of buffered items (must be positive).</param>
/// <param name="allowSynchronousContinuations">
/// When <see langword="true"/> (default), continuations may run inline on the signalling thread; when
/// <see langword="false"/>, they are scheduled asynchronously.
/// </param>
[PublicAPI]
public sealed class SpscChannelReader<T>(int capacity, bool allowSynchronousContinuations = true) : SpscQueue<T>(capacity)
{
    private readonly CompleteSignal _readerSignal = new(allowSynchronousContinuations);

    /// <summary>Raised when a slot of a full buffer is freed (edge-triggered).</summary>
    [PublicAPI]
    public event Action? OnWriteReady;

    /// <summary>Wakes a reader when data or the end of the stream becomes available.</summary>
    protected override void OnFirstInsertOrComplete() => _readerSignal.Signal();

    /// <summary>Clears a stale reader signal when the buffer is drained, so the next write re-raises it.</summary>
    protected override void OnDrained() => _readerSignal.TryReset();

    /// <summary>Raises <see cref="OnWriteReady"/> when a slot of a full buffer is freed.</summary>
    protected override void OnFreeSpace() => OnWriteReady?.Invoke();

    /// <summary>
    /// Waits until a value is available or the stream ends, without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the queue is readable; a completed task is returned
    /// when a value is already available or the stream is over.
    /// </returns>
    protected internal override ValueTask WaitToReadAsync()
    {
        while (true)
        {
            // Data already published, or the stream is over — no need to wait.
            if (Volatile.Read(ref WriterSeq) != ReaderSeq || IsCompleted)
                return ValueTask.CompletedTask;

            // A signal is raised but no data is available yet (it was consumed earlier): clear the
            // stale signal and re-check, so a signal raised between the check and the reset is not lost.
            if (_readerSignal.TryReset())
                continue;

            // No signal raised: register a wait. WaitAsync does not consume a concurrently raised
            // signal — it completes synchronously and the loop re-checks the queue.
            return _readerSignal.WaitAsync();
        }
    }
    
    /// <summary>
    /// Gets the read-side role view of the channel, exposing asynchronous read readiness
    /// (<see cref="ChannelReader{T}.WaitToReadAsync"/>) alongside the core read operations.
    /// </summary>
    /// <returns>A <see cref="ChannelReader{T}"/> that waits for data (or the end of the stream) and reads.</returns>
    public new ChannelReader<T> Reader => new(this);
}
