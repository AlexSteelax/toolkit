using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// A bounded, lock-free single-producer/single-consumer FIFO queue: the writer enqueues via
/// <see cref="TryWrite"/>, the reader consumes via <see cref="TryRead"/>.
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <remarks>
/// <para>
/// Single-producer/single-consumer by design: one writer drives <see cref="TryWrite"/> and
/// <see cref="TryComplete"/>; one reader drives <see cref="TryRead"/>. Access from other threads is
/// not supported and must be avoided.
/// </para>
/// <para>
/// Storage is a fixed-size circular buffer of power-of-two length (rounded up from the requested
/// capacity, which remains the strict upper bound of buffered items). Availability is modelled as a
/// pair of monotonically increasing counters: <c>WriterSeq</c> counts enqueued (and fully published)
/// items, <c>ReaderSeq</c> counts consumed ones. The writer publishes an item (<c>WriterSeq++</c>,
/// release) only after writing it into the ring, and the reader consumes only while the delta
/// <c>WriterSeq - ReaderSeq > 0</c> (empty when equal, full when the delta reaches the capacity).
/// This guarantees the consumed item is fully visible and closes the writer's enqueue→publish window.
/// The counters are <see cref="uint"/> (modular 2³² — wrap-around is natural, all checks use the
/// delta) and double as ring indices via the power-of-two mask.
/// </para>
/// <para>
/// A write into an empty queue raises <see cref="OnFirstInsertOrComplete"/>, a protected virtual hook intended
/// for derived types that layer an external readiness signal on top of the transfer core.
/// </para>
/// <para>
/// Role separation: callers are expected to use the queue through its role views — <see cref="Reader"/>
/// (read side: <see cref="TryRead"/>, <see cref="IsCompleted"/>, <see cref="Count"/>) and <see cref="Writer"/>
/// (write side: <see cref="TryWrite"/>, <see cref="TryComplete"/>, <see cref="Count"/>) — obtained via
/// <see cref="Reader"/> / <see cref="Writer"/>. The underlying operations are <see langword="protected internal"/>:
/// they are exercised by the role views and by derived channel types, not called directly by consumers.
/// Waiting (readiness) is layered by derived types that override <see cref="WaitToReadAsync"/> /
/// <see cref="WaitToWriteAsync"/>; the bare queue does not raise readiness signals and the base implementations
/// throw <see cref="NotImplementedException"/>.
/// </para>
/// </remarks>
public class SpscQueue<T>
{
    private readonly T[] _buffer;
    /// <summary>The maximum number of buffered items.</summary>
    protected readonly uint Capacity;
    private readonly uint _mask;

    private bool _closed;
    private ExceptionDispatchInfo? _error;
    private bool _completed;

    /// <summary>The number of items enqueued and fully published by the writer (written only by the writer).</summary>
    internal uint WriterSeq;

    /// <summary>The number of items consumed by the reader (written only by the reader).</summary>
    internal uint ReaderSeq;

    /// <summary>
    /// Initializes a new bounded, lock-free single-producer/single-consumer queue.
    /// </summary>
    /// <param name="capacity">The maximum number of buffered items (must be positive).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
    public SpscQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Capacity = (uint)capacity;
        _buffer = new T[NextPowerOfTwo((uint)capacity + 1)];
        _mask = (uint)_buffer.Length - 1;
    }

    /// <summary>Rounds <paramref name="value"/> up to the nearest power of two.</summary>
    private static int NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return (int)(value + 1);
    }

    /// <summary>
    /// Gets the number of items currently buffered.
    /// </summary>
    /// <remarks>
    /// A best-effort snapshot: under concurrent access it reflects the delta between the published
    /// (<see cref="WriterSeq"/>) and consumed (<see cref="ReaderSeq"/>) sequences at read time and may
    /// be momentarily off (e.g. an item written but not yet drained). It never underflows and costs no
    /// writes on the hot path — both counters already exist.
    /// </remarks>
    protected internal int Count => (int)(Volatile.Read(ref WriterSeq) - Volatile.Read(ref ReaderSeq));

    /// <summary>
    /// Gets a value indicating whether the buffer currently holds at least one item, i.e. a
    /// <see cref="TryRead"/> would succeed without blocking.
    /// </summary>
    /// <remarks>
    /// A best-effort snapshot under concurrent access (like <see cref="Count"/>): it reflects the
    /// published/consumed counters at read time and may be momentarily off, but it never underflows.
    /// The end of the stream is a separate concept — see <see cref="IsCompleted"/>. Exposed to the
    /// reader role view only.
    /// </remarks>
    protected internal bool IsReadable => Volatile.Read(ref WriterSeq) != ReaderSeq;

    /// <summary>
    /// Gets a value indicating whether the buffer currently has at least one free slot, i.e. a
    /// <see cref="TryWrite"/> would succeed without being rejected.
    /// </summary>
    /// <remarks>
    /// A best-effort snapshot under concurrent access (like <see cref="Count"/>): it reflects the
    /// published/consumed counters at read time and may be momentarily off, but it never reports a free
    /// slot when the buffer is full. Exposed to the writer role view only.
    /// </remarks>
    protected internal bool IsWritable => WriterSeq - Volatile.Read(ref ReaderSeq) < Capacity;

    /// <summary>
    /// Attempts to read a value without blocking.
    /// </summary>
    /// <param name="value">The read value, if available.</param>
    /// <returns>
    /// <see langword="true"/> when a value was read; otherwise, <see langword="false"/>. When
    /// <see langword="false"/> is returned, check <see cref="IsCompleted"/> to distinguish an ended
    /// stream (<see langword="true"/>) from a temporarily empty buffer (<see langword="false"/>).
    /// If the stream was closed with an exception, it is rethrown instead of returning.
    /// </returns>
    protected internal virtual bool TryRead([MaybeNullWhen(false)] out T value)
    {
        var writerSeq = Volatile.Read(ref WriterSeq);

        if (writerSeq == ReaderSeq)
        {
            // The buffer is empty. If the stream is closed, latch the terminal state so IsCompleted
            // becomes observable (Complete may have been called while items were still buffered), then
            // surface the completion exception if one was captured.
            if (Volatile.Read(ref _closed))
            {
                if (Volatile.Read(ref _error) is { } error)
                    error.Throw();

                Volatile.Write(ref _completed, true);
            }

            value = default!;
            return false;
        }

        value = _buffer[ReaderSeq & _mask];
        _buffer[ReaderSeq & _mask] = default!;

        var wasFull = writerSeq - ReaderSeq >= Capacity;
        Volatile.Write(ref ReaderSeq, unchecked(ReaderSeq + 1));

        if (wasFull)
            OnFreeSpace();

        if (writerSeq - ReaderSeq == 0)
            OnDrained();

        return true;
    }

    /// <summary>
    /// Raised when the queue becomes readable: the first item was just written
    /// (<see cref="TryWrite"/> transitioned the buffer from empty to non-empty) or <see cref="TryComplete"/>
    /// closed the stream. Override to wake a reader when data (or the end of the stream) becomes available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnFirstInsertOrComplete() { }
    
    /// <summary>
    /// Raised by <see cref="TryRead"/> when an item was consumed from a full buffer, freeing capacity.
    /// Override to wake a writer waiting to retry a rejected write.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnFreeSpace() { }

    /// <summary>
    /// Raised by <see cref="TryRead"/> when the last buffered item was consumed (the buffer became empty).
    /// Override to clear a raised read-readiness signal so a subsequent write re-raises it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnDrained() { }

    /// <summary>
    /// Raised by <see cref="TryWrite"/> when the write fills the buffer (there is no free capacity left).
    /// Override to clear a raised write-readiness signal so a subsequent read re-raises it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnFilled() { }

    /// <summary>
    /// Waits until a value is available or the stream ends, without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the queue is readable; a completed task is returned
    /// when a value is already available or the stream is over.
    /// </returns>
    /// <exception cref="NotImplementedException">
    /// Thrown by the base implementation: the bare queue does not raise read-readiness signals. Derived
    /// channel types (e.g. <see cref="SpscChannel{T}"/>, <see cref="SpscChannelReader{T}"/>) override this
    /// member to layer readiness on top of the transfer core.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal virtual ValueTask WaitToReadAsync() => throw new NotImplementedException();

    /// <summary>
    /// Waits until capacity is available, without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when the queue has free capacity; a completed task is returned
    /// when there is already room or the stream is over.
    /// </returns>
    /// <exception cref="NotImplementedException">
    /// Thrown by the base implementation: the bare queue does not raise write-readiness signals. Derived
    /// channel types (e.g. <see cref="SpscChannel{T}"/>, <see cref="SpscChannelWriter{T}"/>) override this
    /// member to layer readiness on top of the transfer core.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal virtual ValueTask WaitToWriteAsync() => throw new NotImplementedException();

    /// <summary>
    /// Gets a value indicating whether the stream has ended: <see cref="TryComplete"/> was called and
    /// the buffer is empty (either immediately, or after the reader drained the remaining items).
    /// </summary>
    /// <remarks>
    /// Check this property after <see cref="TryRead"/> returns <see langword="false"/> to distinguish an
    /// ended stream from a temporarily empty buffer.
    /// </remarks>
    protected internal bool IsCompleted => Volatile.Read(ref _completed);

    /// <summary>
    /// Attempts to enqueue an item.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <returns>
    /// <see langword="true"/> if the item was enqueued; <see langword="false"/> when the buffer is
    /// full.
    /// </returns>
    /// <remarks>
    /// Writing to a closed stream is an error and throws. A rejected write (full buffer) is not an
    /// error: the caller may observe the freed capacity (via an external readiness mechanism layered by
    /// a derived type) and retry.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stream has already been closed by <see cref="TryComplete"/>.
    /// </exception>
    protected internal virtual bool TryWrite(T item)
    {
        if (_closed)
            ThrowClosedException();

        var count = WriterSeq - Volatile.Read(ref ReaderSeq);

        if (count >= Capacity)
            return false;

        // Write the item into the ring slot and publish it (release) before signalling: the release
        // write guarantees that a reader, seeing the new WriterSeq via Volatile.Read, also sees the item.
        _buffer[WriterSeq & _mask] = item;
        Volatile.Write(ref WriterSeq, unchecked(WriterSeq + 1));

        if (count == 0)
            OnFirstInsertOrComplete();

        if (count + 1 == Capacity)
            OnFilled();

        return true;
    }

    /// <summary>
    /// Marks the stream as closed: no further writes are accepted and pending or subsequent reads
    /// observe the end of the stream (or the supplied <paramref name="ex"/>).
    /// </summary>
    /// <param name="ex">The exception to surface to the reader, if the stream is faulted.</param>
    /// <returns>
    /// <see langword="true"/> if the stream was closed by this call; <see langword="false"/> when it
    /// was already closed.
    /// </returns>
    protected internal virtual bool TryComplete(Exception? ex = null)
    {
        if (_closed)
            return false;

        if (ex is not null)
            Volatile.Write(ref _error, ExceptionDispatchInfo.Capture(ex));

        Volatile.Write(ref _closed, true);

        if (WriterSeq - Volatile.Read(ref ReaderSeq) == 0)
            Volatile.Write(ref _completed, true);

        OnFirstInsertOrComplete();

        return true;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowClosedException()
    {
        if (Volatile.Read(ref _error) is { } error)
            error.Throw();

        throw new InvalidOperationException("Queue closed");
    }

    /// <summary>
    /// Gets the read-side role view of the queue.
    /// </summary>
    /// <returns>A <see cref="QueueReader{T}"/> exposing the read-side operations (<see cref="TryRead"/>,
    /// <see cref="IsCompleted"/>, <see cref="Count"/>) without granting write access.</returns>
    public QueueReader<T> Reader => new(this);

    /// <summary>
    /// Gets the write-side role view of the queue.
    /// </summary>
    /// <returns>A <see cref="QueueWriter{T}"/> exposing the write-side operations (<see cref="TryWrite"/>,
    /// <see cref="TryComplete"/>, <see cref="Count"/>) without granting read access.</returns>
    public QueueWriter<T> Writer => new(this);
}