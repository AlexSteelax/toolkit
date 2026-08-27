using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Collections;

public partial class Conduit<T>
{
    /// <summary>
    /// Gets a value indicating whether the ring buffer is full — every slot is occupied.
    /// </summary>
    /// <remarks>
    /// <see langword="true"/> only while all <see cref="Capacity"/> slots are filled; it does not
    /// consider the stream's completion (a closed stream may still hold buffered items, so a non-full
    /// buffer does not imply that a write succeeds). Intended as a cheap, side-effect-free, non-throwing
    /// writer-side probe to skip an enqueue iteration when there is no room, e.g.
    /// <c>if (!conduit.IsFull) conduit.TryWrite(item);</c> (a write after completion is still rejected by
    /// <see cref="TryWrite"/> itself). Safe to read from either side.
    /// </remarks>
    [PublicAPI]
    public bool IsFull => WriterSeq - Volatile.Read(ref ReaderSeq) == Capacity;

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
    /// stream is an error and throws. A write into an empty buffer raises <see cref="OnReadReady"/>;
    /// a write that fills the buffer clears the write-readiness signal.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stream has already been closed by <see cref="TryComplete"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWrite(T item)
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
            WakeUpReader();

        if (count + 1 == Capacity)
            _ = _writerSignal?.TryReset();

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
    /// immediately; otherwise the reader first drains the buffered items and the terminal state is latched
    /// by <see cref="TryRead"/>/<see cref="TryPeek"/>. Either way <see cref="OnReadReady"/> is raised so
    /// the reader re-checks the queue.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryComplete(Exception? ex = null)
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
            Close();
        }

        WakeUpReader();

        return true;
    }
    
    /// <summary>
    /// Waits until capacity is available, without blocking the calling thread (writer side only).
    /// </summary>
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
    /// The bare conduit (no <see cref="ConduitBehavior.AwaitableWriter"/>) reports liveness instead of
    /// blocking: it resolves with <see langword="true"/> while the stream is alive. With the awaitable
    /// behavior, an actual write-readiness signal is awaited.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> WaitToWriteAsync()
    {
        if (_writerSignal is null)
            return ValueTask.FromResult(!Volatile.Read(ref _completed));
        
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
            if (_writerSignal.TryReset())
                continue;

            // No signal raised: register a wait. The signal resolves to true (capacity freed) or false
            // (stream completed); a concurrently raised signal completes WaitAsync synchronously and the
            // loop re-checks the queue.
            return _writerSignal.WaitAsync();
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