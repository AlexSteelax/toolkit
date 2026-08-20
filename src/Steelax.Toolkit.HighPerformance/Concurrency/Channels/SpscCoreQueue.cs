using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// The lock-free transfer core shared by the SPSC queue and channel family: a bounded single-producer /
/// single-consumer FIFO over a power-of-two circular buffer.
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <remarks>
/// <para>
/// <b>Side discipline (SPSC).</b> Exactly two parties share an instance. The <em>writer</em> is the
/// single thread calling <see cref="TryWrite"/> and <see cref="TryComplete"/>; the <em>reader</em> is
/// the single thread calling <see cref="TryRead"/>/<see cref="TryPeek"/> and (in derived channel types)
/// <see cref="WaitToReadAsync"/>. Using a side from another thread, or driving both sides from one
/// thread, is not supported. <see cref="Count"/> and <see cref="IsCompleted"/> are safe to read from
/// either side.
/// </para>
/// <para>
/// <b>Memory model.</b> The writer stores an item into the ring and only then publishes it with a
/// release store to <c>WriterSeq</c>; the reader observes items only through an acquire load of
/// <c>WriterSeq</c>, so a consumed item is always fully visible. Availability is the modular delta
/// <c>WriterSeq - ReaderSeq</c> (empty when equal, full when it reaches <see cref="Capacity"/>); the
/// <see cref="uint"/> counters wrap naturally and double as ring indices via the power-of-two mask.
/// </para>
/// <para>
/// <b>Readiness hooks.</b> Transitions raise protected virtual hooks that derived types override to
/// layer an external readiness mechanism (a signal, an event, or both) on top of the transfer core:
/// <see cref="OnReadable"/> (writer → reader), <see cref="OnDrained"/> (reader), <see cref="OnWritable"/>
/// (reader → writer), <see cref="OnFilled"/> (writer), <see cref="OnCompleted"/> (terminal).
/// </para>
/// <para>
/// <b>Liveness and abort.</b> <see cref="Version"/> is a monotonic activity counter for watchdog
/// checks; <see cref="TryTerminate"/> hard-aborts the stream (unlike <see cref="TryComplete"/>, buffered
/// data is not drained and both sides observe the supplied exception immediately).
/// </para>
/// </remarks>
[PublicAPI]
public abstract class SpscCoreQueue<T>
{
    private readonly T[] _buffer;
    private readonly uint _mask;
    
    private bool _closed;
    private ExceptionDispatchInfo? _error;
    private bool _completed;

    private int _version;
    
    internal uint WriterSeq;
    internal uint ReaderSeq;
    
    private static readonly bool IsReferenceOrContainsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    
    /// <summary>
    /// Initializes the core with a fixed-size ring buffer.
    /// </summary>
    /// <param name="capacity">The maximum number of buffered items (must be positive).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not positive.
    /// </exception>
    /// <remarks>
    /// Storage is rounded up to a power of two, but <see cref="Capacity"/> stays the strict upper bound
    /// of buffered items: the ring holds <c>capacity + 1</c> slots so the empty and full states remain
    /// distinguishable.
    /// </remarks>
    protected SpscCoreQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Capacity = (uint)capacity;
        _buffer = new T[NextPowerOfTwo((uint)capacity + 1)];
        _mask = (uint)_buffer.Length - 1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
    
    /// <summary>
    /// The maximum number of buffered items.
    /// </summary>
    /// <remarks>
    /// A strict upper bound: writes are rejected once the buffer holds this many items, even though the
    /// underlying ring has one extra slot (used to tell empty from full). Safe to read from either side.
    /// </remarks>
    [PublicAPI]
    public readonly uint Capacity;
    
    /// <summary>
    /// Gets a monotonically increasing activity counter, useful for watchdog liveness checks.
    /// </summary>
    /// <remarks>
    /// Incremented by the writer on each successful write and on completion (<see cref="TryComplete"/>),
    /// and by the reader while draining the tail of a closed stream. A watchdog can compare snapshots of
    /// this value over a timeout to detect a stalled producer/consumer and call
    /// <see cref="TryTerminate"/> to hard-abort the queue.
    /// </remarks>
    [PublicAPI]
    public int Version => Volatile.Read(ref _version);
    
    /// <summary>
    /// Gets the number of items currently buffered.
    /// </summary>
    /// <remarks>
    /// A best-effort snapshot of the delta between the published (<c>WriterSeq</c>) and consumed
    /// (<c>ReaderSeq</c>) sequences. It never underflows, is safe to read from either side, and costs no
    /// writes on the hot path — both counters already exist.
    /// </remarks>
    [PublicAPI]
    public int Count => (int)(Volatile.Read(ref WriterSeq) - Volatile.Read(ref ReaderSeq));
    
    /// <summary>
    /// Gets a value indicating whether the stream has ended: <see cref="TryComplete"/> was called and
    /// the buffer is empty (immediately, or after the reader drained the remaining items).
    /// </summary>
    /// <remarks>
    /// Safe to read from either side. The reader can use it to distinguish an ended stream from a
    /// temporarily empty buffer after <see cref="TryRead"/> returns <see langword="false"/>.
    /// </remarks>
    [PublicAPI]
    public bool IsCompleted => Volatile.Read(ref _completed);
    
    /// <summary>
    /// Raised on the <em>writer</em> side when the queue becomes readable: the first item was just
    /// written (<see cref="TryWrite"/> transitioned the buffer from empty to non-empty) or the stream was
    /// completed (<see cref="TryComplete"/>), so a reader should re-check the queue. Override to wake a
    /// reader.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnReadable() { }
    
    /// <summary>
    /// Raised on the <em>reader</em> side by <see cref="TryRead"/> when an item was consumed from a full
    /// buffer, freeing a slot. Override to wake a writer waiting to retry a rejected write.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnWritable() { }
    
    /// <summary>
    /// Raised on the <em>reader</em> side by <see cref="TryRead"/> when the last buffered item was
    /// consumed (the buffer became empty). Override to clear a raised read-readiness signal so a
    /// subsequent write re-raises it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnDrained() { }
    
    /// <summary>
    /// Raised on the <em>writer</em> side by <see cref="TryWrite"/> when the write fills the buffer
    /// (no free capacity left). Override to clear a raised write-readiness signal so a subsequent read
    /// re-raises it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnFilled() { }
    
    /// <summary>
    /// Raised on the <em>reader</em> side when the stream reaches its terminal state: the buffer is
    /// empty and the stream is closed. This is the only place the terminal latch is armed. Override to
    /// latch completion on a readiness signal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnCompleted() { }
    
    /// <summary>
    /// Attempts to read a value without blocking (reader side only).
    /// </summary>
    /// <param name="value">The read value, if available.</param>
    /// <returns>
    /// <see langword="true"/> when a value was read; otherwise <see langword="false"/>. When
    /// <see langword="false"/>, check <see cref="IsCompleted"/> to distinguish an ended stream from a
    /// temporarily empty buffer. If the stream was closed with an exception, it is rethrown instead of
    /// returning.
    /// </returns>
    /// <remarks>
    /// Only the reader thread may call this method. Consuming from a full buffer frees a slot and raises
    /// <see cref="OnWritable"/>; consuming the last item raises <see cref="OnDrained"/>. On an empty
    /// buffer of a closed stream the terminal state is latched (<see cref="OnCompleted"/>) so
    /// <see cref="IsCompleted"/> becomes observable.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryRead([MaybeNullWhen(false)] out T value)
    {
        var count = Volatile.Read(ref WriterSeq) - ReaderSeq;
        
        if (CompleteOrThrowClosedException(count == 0))
        {
            value = default!;
            return false;
        }

        var index = ReaderSeq & _mask;

        value = _buffer[index];
        
        if (IsReferenceOrContainsReferences)
            _buffer[index] = default!;
        
        Volatile.Write(ref ReaderSeq, unchecked(ReaderSeq + 1));

        // While a closed stream still has buffered items, the reader is the side making progress,
        // so it must advance the liveness counter (the writer is done).
        if (Volatile.Read(ref _closed))
            Interlocked.Increment(ref _version);

        if (count == Capacity)
            OnWritable();

        if (count == 1)
            OnDrained();

        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CompleteOrThrowClosedException(bool maybeCompleted)
    {
        var completed = Volatile.Read(ref _completed);
        
        if (completed)
        {
            if (Volatile.Read(ref _error) is { } error)
                error.Throw();

            return true;
        }
        
        // The buffer is empty. If the stream is closed, latch the terminal state so IsCompleted
        // becomes observable (Complete may have been called while items were still buffered), then
        // surface the completion exception if one was captured.
        if (!maybeCompleted)
            return maybeCompleted;
        
        {
            // Mirror the terminal latch of TryRead so the completion exception surfaces to the reader
            // and IsCompleted becomes observable.
            if (!Volatile.Read(ref _closed))
                return maybeCompleted;
            
            if (Volatile.Read(ref _error) is { } error)
                error.Throw();

            if (completed)
                return maybeCompleted;
            
            Volatile.Write(ref _completed, true);
            OnCompleted();
        }

        return maybeCompleted;
    }
    
    /// <summary>
    /// Peeks the value at the head of the buffer without consuming it (reader side only).
    /// </summary>
    /// <param name="value">The value at the head of the buffer, if available.</param>
    /// <returns>
    /// <see langword="true"/> when a value is available; otherwise <see langword="false"/>. The terminal
    /// behavior mirrors <see cref="TryRead"/>: on an empty buffer of a closed stream the completion
    /// exception is rethrown and <see cref="IsCompleted"/> becomes observable.
    /// </returns>
    /// <remarks>
    /// Only the reader thread may call this method. Unlike <see cref="TryRead"/>, it neither advances the
    /// reader sequence nor clears the slot, so it never raises the readiness hooks and may be called
    /// repeatedly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryPeek([MaybeNullWhen(false)] out T value)
    {
        var count = Volatile.Read(ref WriterSeq) - ReaderSeq;

        if (CompleteOrThrowClosedException(count == 0))
        {
            value = default!;
            return false;
        }

        var index = ReaderSeq & _mask;
        value = _buffer[index];
        return true;
    }
    
    /// <summary>
    /// Waits until a value is available or the stream ends, without blocking the calling thread
    /// (reader side only).
    /// </summary>
    /// <param name="readerSignal">
    /// The readiness signal the derived type wires to the reader; the signal is raised by the writer via
    /// <see cref="OnReadable"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with <see langword="true"/> when the queue is
    /// readable, or <see langword="false"/> when the stream has ended.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Only the reader thread may call this method. The loop re-checks the queue after clearing a stale
    /// signal (<see cref="CompleteSignal.TryReset"/>), so a signal raised between a failed check and a
    /// reset is never lost.
    /// </para>
    /// <para>
    /// Derived types should call this with their own signal instance; the bare queue does not raise
    /// readiness and the synchronous leaf (<see cref="SpscQueue{T}"/>) does not expose waiting at all.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected ValueTask<bool> WaitToReadAsync(CompleteSignal readerSignal)
    {
        while (true)
        {
            // The stream is over — no need to wait.
            if (IsCompleted)
                return ValueTask.FromResult(false);
            
            // Data already published — no need to wait.
            if (Volatile.Read(ref WriterSeq) != ReaderSeq)
                return ValueTask.FromResult(true);

            // A signal is raised but no data is available yet (it was consumed earlier): clear the
            // stale signal and re-check, so a signal raised between the check and the reset is not lost.
            if (readerSignal.TryReset())
                continue;

            // No signal raised: register a wait. The signal resolves to true (data arrived) or false
            // (stream completed); a concurrently raised signal completes WaitAsync synchronously and the
            // loop re-checks the queue.
            return readerSignal.WaitAsync();
        }
    }
    
    /// <summary>
    /// Attempts to enqueue an item (writer side only).
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <returns>
    /// <see langword="true"/> if the item was enqueued; <see langword="false"/> when the buffer is full.
    /// </returns>
    /// <remarks>
    /// Only the writer thread may call this method. A rejected write (full buffer) is not an error: the
    /// caller may observe the freed capacity via a readiness mechanism and retry. Writing to a closed
    /// stream is an error and throws. A write into an empty buffer raises <see cref="OnReadable"/>; a
    /// write that fills the buffer raises <see cref="OnFilled"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stream has already been closed by <see cref="TryComplete"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryWrite(T item)
    {
        if (_closed)
            ThrowClosedException();

        var count = WriterSeq - Volatile.Read(ref ReaderSeq);

        if (count == Capacity)
            return false;

        // Write the item into the ring slot and publish it (release) before signalling: the release
        // write guarantees that a reader, seeing the new WriterSeq via Volatile.Read, also sees the item.
        var index = WriterSeq & _mask;
        _buffer[index] = item;
        Volatile.Write(ref WriterSeq, unchecked(WriterSeq + 1));
        _version++;

        if (count == 0)
            OnReadable();

        if (count + 1 == Capacity)
            OnFilled();

        return true;
    }
    
    /// <summary>
    /// Marks the stream as closed (writer side only): no further writes are accepted and pending or
    /// subsequent reads observe the end of the stream (or the supplied <paramref name="ex"/>).
    /// </summary>
    /// <param name="ex">The exception to surface to the reader, if the stream is faulted.</param>
    /// <returns>
    /// <see langword="true"/> if the stream was closed by this call; <see langword="false"/> when it was
    /// already closed.
    /// </returns>
    /// <remarks>
    /// Only the writer thread may call this method. If the buffer is empty the terminal state is latched
    /// immediately (<see cref="OnCompleted"/>); otherwise the reader first drains the buffered items and
    /// the terminal state is latched by <see cref="TryRead"/>/<see cref="TryPeek"/>. Either way
    /// <see cref="OnReadable"/> is raised so the reader re-checks the queue.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryComplete(Exception? ex = null)
    {
        if (_closed)
            return false;

        if (ex is not null)
            Volatile.Write(ref _error, ExceptionDispatchInfo.Capture(ex));

        _version++;
        
        Volatile.Write(ref _closed, true);

        if (WriterSeq - Volatile.Read(ref ReaderSeq) == 0)
        {
            Volatile.Write(ref _completed, true);
            OnCompleted();
        }

        OnReadable();

        return true;
    }

    /// <summary>
    /// Hard-aborts the stream: immediately latches completion and surfaces <paramref name="ex"/> on the
    /// next read/write, without draining any buffered items.
    /// </summary>
    /// <param name="ex">The exception to surface to both sides.</param>
    /// <returns>
    /// <see langword="true"/> if the stream was terminated by this call; <see langword="false"/> when it
    /// was already terminally completed.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="TryComplete"/>, this is an <em>abrupt</em> stop: buffered data is not consumed
    /// and the reader observes <paramref name="ex"/> immediately (via <see cref="TryRead"/>/<see cref="TryPeek"/>),
    /// while the writer observes it via <see cref="TryWrite"/>. Intended to be called from a watchdog
    /// thread that has detected a stall (<see cref="Version"/> did not advance).
    /// </remarks>
    protected bool TryTerminate(Exception ex)
    {
        if (Volatile.Read(ref _completed))
            return false;
        
        // Override because it's hard aborting
        Volatile.Write(ref _error, ExceptionDispatchInfo.Capture(ex));
        Volatile.Write(ref _closed, true);
        Volatile.Write(ref _completed, true);
        
        OnCompleted();
        OnReadable();
        OnWritable();
        
        return true;
    }
    
    /// <summary>
    /// Waits until capacity is available, without blocking the calling thread (writer side only).
    /// </summary>
    /// <param name="writerSignal">
    /// The readiness signal the derived type wires to the writer; the signal is raised by the reader via
    /// <see cref="OnWritable"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with <see langword="true"/> when the queue has
    /// free capacity, or <see langword="false"/> when the stream has ended.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Only the writer thread may call this method. The loop re-checks the queue after clearing a stale
    /// signal (<see cref="CompleteSignal.TryReset"/>), so a signal raised between a failed check and a
    /// reset is never lost.
    /// </para>
    /// <para>
    /// Derived types should call this with their own signal instance; the bare queue does not raise
    /// readiness and the synchronous leaf (<see cref="SpscQueue{T}"/>) does not expose waiting at all.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected ValueTask<bool> WaitToWriteAsync(CompleteSignal writerSignal)
    {
        while (true)
        {
            // The stream is over — no need to wait.
            if (IsCompleted)
                return ValueTask.FromResult(false);

            // Room is already available — no need to wait.
            if (WriterSeq - Volatile.Read(ref ReaderSeq) < Capacity)
                return ValueTask.FromResult(true);

            // A signal is raised but no room is available yet (it was consumed earlier): clear the
            // stale signal and re-check, so a signal raised between the check and the reset is not lost.
            if (writerSignal.TryReset())
                continue;

            // No signal raised: register a wait. The signal resolves to true (capacity freed) or false
            // (stream completed); a concurrently raised signal completes WaitAsync synchronously and the
            // loop re-checks the queue.
            return writerSignal.WaitAsync();
        }
    }
    
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowClosedException()
    {
        if (Volatile.Read(ref _error) is { } error)
            error.Throw();

        throw new InvalidOperationException("Queue closed");
    }
}