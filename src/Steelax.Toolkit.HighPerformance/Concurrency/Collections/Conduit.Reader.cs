using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Collections;

public partial class Conduit<T>
{
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
    /// Only the reader thread may call this method. Consuming from a full buffer frees a slot and
    /// raises <see cref="OnWriteReady"/>; consuming the last item clears the read-readiness signal. On
    /// an empty buffer of a closed stream the terminal state is latched so <see cref="IsCompleted"/>
    /// becomes observable.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead([MaybeNullWhen(false)] out T value)
    {
        var count = Volatile.Read(ref WriterSeq) - ReaderSeq;
        
        if (CompleteOrThrowClosedException(count == 0))
        {
            value = default!;
            _ = Interlocked.CompareExchange(ref ReaderMiss, 1, 0);
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
        
        if (Interlocked.CompareExchange(ref WriterMiss, 0, 1) == 1 || count == Capacity)
            WakeUpWriter();

        return true;
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
    /// reader sequence nor clears the slot, so it never raises readiness events and may be called
    /// repeatedly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeek([MaybeNullWhen(false)] out T value)
    {
        var count = Volatile.Read(ref WriterSeq) - ReaderSeq;

        if (CompleteOrThrowClosedException(count == 0))
        {
            value = default!;
            _ = Interlocked.CompareExchange(ref ReaderMiss, 1, 0);
            return false;
        }

        var index = ReaderSeq & _mask;
        value = _buffer[index];
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CompleteOrThrowClosedException(bool maybeCompleted)
    {
        var completed = Volatile.Read(ref _completed);

        if (!completed && Volatile.Read(ref _closed) && maybeCompleted)
        {
            Volatile.Write(ref _completed, true);
            completed = true;
        }

        if (completed)
        {
            _readerSignal?.Complete();
            
            if (Volatile.Read(ref _error) is { } error)
                error.Throw();

            return true;
        }

        return maybeCompleted;
    }
    
    /// <summary>
    /// Waits until a value is available or the stream ends, without blocking the calling thread
    /// (reader side only).
    /// </summary>
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
    /// The bare conduit (no <see cref="ConduitBehavior.AwaitableReader"/>) reports liveness instead of
    /// blocking: it resolves with <see langword="true"/> while the stream is alive. With the awaitable
    /// behavior, an actual read-readiness signal is awaited.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> WaitToReadAsync()
    {
        if (_readerSignal is null)
            return ValueTask.FromResult(!Volatile.Read(ref _completed));
        
        // The stream is over — no need to wait.
        if (IsCompleted)
            return ValueTask.FromResult(false);
            
        // Data already published — no need to wait.
        if (Volatile.Read(ref WriterSeq) != ReaderSeq)
            return ValueTask.FromResult(true);

        // No signal raised: register a wait. The signal resolves to true (data arrived) or false
        // (stream completed); a concurrently raised signal completes WaitAsync synchronously and the
        // loop re-checks the queue.
        return _readerSignal.WaitAsync();
    }
}