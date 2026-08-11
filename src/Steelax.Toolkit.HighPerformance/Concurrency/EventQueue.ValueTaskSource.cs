using System.Threading.Tasks.Sources;

namespace Steelax.Toolkit.HighPerformance.Concurrency;

public partial class EventQueue<T>
{
    /// <summary>
    /// Charges the read-side readiness signal, waking an awaiting reader. Called by the writer after an
    /// item has been written into the ring (and published via <c>WriterSeq++</c>) or after
    /// <see cref="Complete"/>. At most one signal per cycle: <see cref="_state"/> is set on charge
    /// and cleared by the reader when the buffer drains. The transition is atomic (CAS); a lost CAS is
    /// safe because the reader re-checks the queue before registering a wait.
    /// </summary>
    private void SignalReadReady()
    {
        var current = Volatile.Read(ref _state);

        // Already signaled — nothing to do.
        if (current == 2)
        {
            return;
        }

        // Idle → Signaled without SetResult: no waiter is registered, so the charge is merely a
        // "data is available" flag. The reader sees 2 and returns immediately; when a read fails
        // because the buffer is empty it clears the signal itself.
        if (current == 0)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
            {
                return;
            }

            current = Volatile.Read(ref _state);
        }

        // Waiting → Signaled: wake the registered waiter. Reset is never done here — the reader's
        // WaitToReadAsync rearms the core, so SetResult always targets an un-completed core.
        if (current == 1)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 1) == 1)
            {
                _readCore.SetResult(true);
            }
        }
    }

    void IValueTaskSource.GetResult(short token)
    {
        _readCore.GetResult(token);
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _readCore.GetStatus(token);

    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _readCore.OnCompleted(continuation, state, token, flags);
}
