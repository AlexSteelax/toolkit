using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// A bounded, lock-free SPSC channel: an <see cref="SpscQueue{T}"/> extended with asynchronous
/// read/write readiness (<see cref="WaitToReadAsync"/> / <see cref="WaitToWriteAsync"/>).
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <param name="capacity">The maximum number of buffered items (must be positive).</param>
/// <param name="allowSynchronousContinuations">
/// When <see langword="true"/> (default), continuations may run inline on the signalling thread; when
/// <see langword="false"/>, they are scheduled asynchronously.
/// </param>
[PublicAPI]
public sealed class SpscChannel<T>(int capacity, bool allowSynchronousContinuations = true) : SpscQueue<T>(capacity)
{
    private readonly CompleteSignal _readerSignal = new(allowSynchronousContinuations);
    private readonly CompleteSignal _writerSignal = new(allowSynchronousContinuations);

    /// <summary>Wakes a reader when data or the end of the stream becomes available.</summary>
    protected override void OnFirstInsertOrComplete() => _readerSignal.Signal();

    /// <summary>Clears a stale reader signal when the buffer is drained, so the next write re-raises it.</summary>
    protected override void OnDrained() => _readerSignal.TryReset();

    /// <summary>Wakes a writer when a slot of a full buffer is freed.</summary>
    protected override void OnFreeSpace() => _writerSignal.Signal();

    /// <summary>Clears a stale writer signal when the buffer is filled, so the next read re-raises it.</summary>
    protected override void OnFilled() => _writerSignal.TryReset();

    /// <summary>
    /// Waits until a value is available or the stream ends, without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the queue is readable; a completed task is returned
    /// when a value is already available or the stream is over.
    /// </returns>
    [PublicAPI]
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
    /// Waits until capacity is available, without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the queue has free capacity; a completed task is
    /// returned when there is already room or the stream is over.
    /// </returns>
    [PublicAPI]
    protected internal override ValueTask WaitToWriteAsync()
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
    
    /// <summary>
    /// Gets the read-side role view of the channel, exposing asynchronous read readiness
    /// (<see cref="ChannelReader{T}.WaitToReadAsync"/>) alongside the core read operations.
    /// </summary>
    /// <returns>A <see cref="ChannelReader{T}"/> that waits for data (or the end of the stream) and reads.</returns>
    public new ChannelReader<T> Reader => new(this);
    
    /// <summary>
    /// Gets the write-side role view of the channel, exposing asynchronous write readiness
    /// (<see cref="ChannelWriter{T}.WaitToWriteAsync"/>) alongside the core write operations.
    /// </summary>
    /// <returns>A <see cref="ChannelWriter{T}"/> that waits for free capacity, writes, and completes the stream.</returns>
    public new ChannelWriter<T> Writer => new(this);
}