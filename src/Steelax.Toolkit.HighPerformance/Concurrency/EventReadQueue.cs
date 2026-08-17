using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency;

/// <summary>
/// A bounded, lock-free SPSC queue for subscription-based reading: the reader observes readiness via
/// <see cref="OnReadReady"/> (raised when data or the end of the stream becomes available), while the
/// writer waits via <see cref="WaitToWriteAsync"/>.
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <param name="capacity">The maximum number of buffered items (must be positive).</param>
/// <param name="allowSynchronousContinuations">
/// When <see langword="true"/> (default), continuations may run inline on the signalling thread; when
/// <see langword="false"/>, they are scheduled asynchronously.
/// </param>
[PublicAPI]
public class EventReadQueue<T>(int capacity, bool allowSynchronousContinuations = true) : SpscQueue<T>(capacity)
{
    private readonly CompleteSignal _writerSignal = new(allowSynchronousContinuations);

    /// <summary>Raised when a value becomes available or the stream ends (edge-triggered).</summary>
    [PublicAPI]
    public event Action? OnReadReady;

    /// <summary>Raises <see cref="OnReadReady"/> when the queue becomes readable.</summary>
    protected override void OnFirstInsertOrComplete() => OnReadReady?.Invoke();

    /// <summary>Wakes a writer when a slot of a full buffer is freed.</summary>
    protected override void OnFreeSpace() => _writerSignal.Signal();

    /// <summary>Clears a stale writer signal when the buffer is filled, so the next read re-raises it.</summary>
    protected override void OnFilled() => _writerSignal.TryReset();

    /// <summary>
    /// Waits until capacity is available, without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the queue has free capacity; a completed task is
    /// returned when there is already room or the stream is over.
    /// </returns>
    [PublicAPI]
    public ValueTask WaitToWriteAsync()
    {
        while (true)
        {
            // Room is already available, or the stream is over — no need to wait.
            if (WriterSeq - Volatile.Read(ref ReaderSeq) < Capacity || IsCompleted)
                return ValueTask.CompletedTask;

            // A signal is raised but no room is available yet (it was consumed earlier): clear the
            // stale signal and re-check, so a signal raised between the check and the reset is not lost.
            if (_writerSignal.TryReset())
                continue;

            // No signal raised: register a wait. WaitAsync does not consume a concurrently raised
            // signal — it completes synchronously and the loop re-checks the queue.
            return _writerSignal.WaitAsync();
        }
    }
}
