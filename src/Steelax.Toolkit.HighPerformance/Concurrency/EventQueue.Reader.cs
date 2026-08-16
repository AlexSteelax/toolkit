using System.Diagnostics.CodeAnalysis;

namespace Steelax.Toolkit.HighPerformance.Concurrency;

public partial class EventQueue<T>
{
    /// <summary>
    /// Attempts to read a value without blocking.
    /// </summary>
    /// <param name="value">The read value, if available.</param>
    /// <returns>
    /// <see langword="true"/> when a value was read; otherwise, <see langword="false"/>. When
    /// <see langword="false"/> is returned, check <see cref="IsCompleted"/> to distinguish an ended
    /// stream (<see langword="true"/>) from a temporarily empty buffer (<see langword="false"/>).
    /// If a completion exception was captured, it is rethrown instead of returning.
    /// </returns>
    [PublicAPI]
    public bool TryRead([MaybeNullWhen(false)] out T value)
    {
        if (Consume(out value!))
            return true;

        if (Volatile.Read(ref _error) is { } error)
            error.Throw();

        if (Volatile.Read(ref _eof))
        {
            _completed = true;
            return false;
        }

        // No data available: clear the raised flag so _state does not stick at "signaled" while the
        // buffer is actually empty. The signal is only cleared here, on a failed read — never after a
        // successful consume. The CAS loop clears 2 → 0 only when data is still absent, so a signal
        // raised by the writer between the failed consume and the clear is never lost.
        // _completed has already been checked above (we return early when it is set), so only "data
        // available" matters here. Checking it again would be wrong: it could report success without
        // any data when the stream completes concurrently.

        while (Volatile.Read(ref _state) == 2)
        {
            if (Volatile.Read(ref WriterSeq) != ReaderSeq)
            {
                // Data has arrived in the meantime — consume it so a valid value is returned
                // (a bare "true" without a value would corrupt the caller's collection).
                if (Consume(out value!))
                    return true;

                continue;
            }

            if (Interlocked.CompareExchange(ref _state, 0, 2) == 2)
                return false;
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the stream has ended: <see cref="Complete"/> was called (with or
    /// without an exception) and the terminal state has been observed by the reader. The value is
    /// <see langword="true"/> only after a failed <see cref="TryRead"/> observes the end of the stream;
    /// it is not raised immediately when <c>Complete</c> is called while data is still buffered.
    /// </summary>
    /// <remarks>
    /// Check this property after <see cref="TryRead"/> returns <see langword="false"/> to distinguish an
    /// ended stream from a temporarily empty buffer.
    /// </remarks>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Waits asynchronously until a value is available or the stream ends, without blocking the
    /// calling thread.
    /// </summary>
    /// <remarks>
    /// After the wait completes, call <see cref="TryRead"/> to obtain the value (or the terminal state).
    /// </remarks>
    [PublicAPI]
    public ValueTask WaitToReadAsync()
    {
        // Data already published, or the stream is over — no need to wait.
        if (Volatile.Read(ref WriterSeq) != ReaderSeq || Volatile.Read(ref _eof))
            return ValueTask.CompletedTask;

        switch (Volatile.Read(ref _state))
        {
            // A signal is raised: data is available. TryRead consumes it and clears the signal only
            // when the buffer turns out to be empty, so there is nothing to reset here.
            case 2:
                return ValueTask.CompletedTask;

            // Idle: rearm the core to a fresh version and register this wait. Reset happens only here
            // (never in Consume or the writer), so the version the reader registers on is guaranteed
            // fresh, and a writer-side SetResult (1 → 2) always targets an un-completed core.
            case 0:
                _readCore.Reset();

                if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
                    return ValueTask.CompletedTask;

                // ReaderTrace.Enqueue($"Wait case0 registered: version={_readCore.Version} WriterSeq={WriterSeq} ReaderSeq={ReaderSeq}");
                return new ValueTask(this, _readCore.Version);

            // case 1: a wait is already registered (a duplicate WaitToReadAsync call). There is
            // nothing to (re)arm — the existing registration will be signaled by the writer.
            default:
                return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Consumes one published item, releasing the occupied capacity and advancing <see cref="ReaderSeq"/>.
    /// </summary>
    /// <param name="value">The consumed item, when one was available.</param>
    /// <returns>
    /// <see langword="true"/> when an item was consumed; <see langword="false"/> when the writer has not
    /// published anything new yet.
    /// </returns>
    /// <remarks>
    /// Consumption is allowed only while the writer has published more than the reader has consumed
    /// (<c>WriterSeq != ReaderSeq</c>): the writer publishes (<c>WriterSeq++</c>, release) only after
    /// writing the item, so a visible item is always fully ready and a signal can never cover an
    /// already-consumed item. This method never touches <c>_state</c> — the signal is cleared by
    /// <see cref="TryRead"/> only when a read fails because the buffer is empty.
    /// </remarks>
    private bool Consume([MaybeNullWhen(false)] out T value)
    {
        var writerSeq = Volatile.Read(ref WriterSeq);

        if (writerSeq == ReaderSeq)
        {
            value = default!;
            return false;
        }

        value = _buffer[ReaderSeq & _mask];
        _buffer[ReaderSeq & _mask] = default!;

        // OnWriteReady is the "capacity freed" signal: a producer could only have been waiting
        // if the buffer was full (writerSeq - ReaderSeq == capacity) before this consume.
        var wasFull = writerSeq - ReaderSeq >= _capacity;
        Volatile.Write(ref ReaderSeq, unchecked(ReaderSeq + 1));

        if (wasFull)
            OnWriteReady?.Invoke();

        return true;
    }
}