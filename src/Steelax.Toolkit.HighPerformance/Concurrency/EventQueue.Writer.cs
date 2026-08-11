namespace Steelax.Toolkit.HighPerformance.Concurrency;

public partial class EventQueue<T>
{
    /// <summary>
    /// Attempts to enqueue an item and signals the reader.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <returns>
    /// <see langword="true"/> if the item was enqueued; <see langword="false"/> when the buffer is
    /// full or already completed.
    /// </returns>
    /// <remarks>
    /// The caller must observe <see cref="OnWriteReady"/> and re-check capacity before retrying a
    /// rejected write.
    /// </remarks>
    [PublicAPI]
    public bool TryWrite(T item)
    {
        // _completed is the writer's own variable (plain read);
        // ReaderSeq is written by the reader, so Volatile.Read is required for the full check.
        if (_completed)
            return false;

        if (WriterSeq - Volatile.Read(ref ReaderSeq) >= _capacity)
            return false;

        // Write the item into the ring slot and publish it (release) before signalling: the release
        // write guarantees that a reader, seeing the new WriterSeq via Volatile.Read, also sees the item.
        _buffer[WriterSeq & _mask] = item;
        Volatile.Write(ref WriterSeq, unchecked(WriterSeq + 1));

        SignalReadReady();

        return true;
    }

    /// <summary>
    /// Marks the stream as completed: no further writes are accepted and pending or subsequent reads
    /// observe the end of the stream (or the supplied <paramref name="ex"/>).
    /// </summary>
    /// <param name="ex">The exception to surface to the reader, if the stream is faulted.</param>
    [PublicAPI]
    public void Complete(Exception? ex = null)
    {
        Volatile.Write(ref _exception, ex);
        Volatile.Write(ref _completed, true);
        SignalReadReady();
    }
}
