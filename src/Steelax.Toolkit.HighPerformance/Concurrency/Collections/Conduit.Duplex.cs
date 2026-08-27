using System.Runtime.ExceptionServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Collections;

public partial class Conduit<T>
{
    /// <summary>
    /// Gets a monotonically increasing activity counter, useful for watchdog liveness checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Incremented by the writer on each successful write and on completion (<see cref="TryComplete"/>),
    /// and by the reader while draining the tail of a closed stream. A watchdog can compare snapshots of
    /// this value over a timeout to detect a stalled producer/consumer and call
    /// <see cref="TryTerminate"/> to hard-abort the queue.
    /// </para>
    /// <para>
    /// The increment is intentionally non-atomic: at the boundary where both sides advance (a closed
    /// stream being drained), the writer and the reader may increment the same base value and lose one
    /// update. The counter then still reflects "at least as much activity" and never corrupts, which is
    /// all a liveness heuristic requires — this is not a precise operation counter.
    /// </para>
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
    [PublicAPI]
    public bool TryTerminate(Exception ex)
    {
        if (Volatile.Read(ref _completed))
            return false;
        
        // Override because it's hard aborting
        Volatile.Write(ref _error, ExceptionDispatchInfo.Capture(ex));
        Volatile.Write(ref _closed, true);
        Volatile.Write(ref _completed, true);
        
        Close();
        WakeUpReader();
        WakeUpWriter();
        
        return true;
    }
}