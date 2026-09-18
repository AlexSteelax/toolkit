using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// A single-consumer readiness signal backed by an <see cref="IValueTaskSource"/>: a producer raises
/// <see cref="Signal"/>, an awaiting consumer wakes via <see cref="WaitAsync"/>, and the raised signal
/// is consumed via <see cref="TryReset"/> (edge-triggered).
/// </summary>
/// <remarks>
/// <para>
/// A single <c>int</c> packs the entire handshake state, so no lock is needed — every operation is a
/// bounded CAS loop:
/// <list type="bullet">
/// <item><c>0x1 (Ready)</c> — a readiness event is pending (edge-triggered). Raised by
/// <see cref="Signal"/> when no waiter is registered; consumed by <see cref="TryReset"/> and by the
/// waiter's fast path.</item>
/// <item><c>0x2 (Wait)</c> — a waiter is registered on the current version of
/// <see cref="ManualResetValueTaskSourceCore{TResult}"/>. Raised by <see cref="WaitAsync"/>; claimed by
/// <see cref="Signal"/> to deliver exactly one wake.</item>
/// </list>
/// Delivery is single-channel: a signal either claims the <c>Wait</c> bit and calls
/// <c>SetResult</c>, or raises the <c>Ready</c> bit for a future fast path. Both can never be held at
/// once by the same event, so neither double-delivery nor lost-wakeup can occur.
/// </para>
/// <para>
/// <see cref="ManualResetValueTaskSourceCore{TResult}.SetResult"/> is always invoked outside the CAS
/// loop: a synchronous continuation (<c>allowSynchronousContinuations</c>) may re-enter
/// <see cref="WaitAsync"/> on the same thread.
/// </para>
/// <para>
/// <see cref="Complete"/> latches the terminal completion flag, making every subsequent
/// <see cref="WaitAsync"/> return <see langword="false"/>, and wakes a registered waiter immediately.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class CompleteSignal(bool allowSynchronousContinuations = true) : IValueTaskSource, IValueTaskSource<bool>
{
    private const int Ready = 0x1;
    private const int Wait = 0x2;

    // Packed handshake state: 0 = idle, Ready = event pending, Wait = waiter registered.
    private int _handshake;

    // Terminal completion latch: set once by Complete(), never cleared, read via Volatile.
    private bool _completed;

    private ManualResetValueTaskSourceCore<bool> _core = new()
    {
        RunContinuationsAsynchronously = !allowSynchronousContinuations
    };

    /// <summary>
    /// Waits for the signal to be raised without blocking the calling thread.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with <see langword="true"/> when readiness was
    /// signalled, or <see langword="false"/> when the signal was completed (terminal).
    /// </returns>
    /// <remarks>
    /// Completes synchronously when a readiness event is already pending, without consuming it —
    /// consumption is done separately via <see cref="TryReset"/>. Await each returned
    /// <see cref="ValueTask{TResult}"/> only once. A completed (terminal) signal always yields
    /// <see langword="false"/>.
    /// </remarks>
    [PublicAPI]
    public ValueTask<bool> WaitAsync()
    {
        // Completed (terminal): always report completion.
        if (Volatile.Read(ref _completed))
            return ValueTask.FromResult(false);

        // Fast path: a readiness event is already pending. Consume it synchronously without touching
        // the core, entirely outside any CAS loop.
        while (true)
        {
            var state = Volatile.Read(ref _handshake);

            // A readiness event is pending: consume it synchronously and report it, without touching
            // the core. This is the same semantic path both on entry and when a signal lands while we
            // are re-arming — no need for a "virtual" pending task.
            if ((state & Ready) != 0)
            {
                if (Interlocked.CompareExchange(ref _handshake, state & ~Ready, state) != state)
                    continue;

                return ValueTask.FromResult(!Volatile.Read(ref _completed));
            }

            // Register as the waiter. Re-arm the core to a fresh version just before claiming the
            // Wait bit: only this thread owns the core, so there is no competing Reset. A Signal that
            // lands between the state read and this CAS either loses the CAS (we regain the loop and
            // observe Ready) or, if the Wait bit wins, claims the Wait bit and delivers SetResult.
            _core.Reset();

            if (Interlocked.CompareExchange(ref _handshake, state | Wait, state) == state)
                return new ValueTask<bool>(this, _core.Version);
        }
    }

    /// <summary>
    /// Consumes a raised readiness signal without waiting.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a readiness signal was pending (now cleared); otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// A completed (terminal) signal is never cleared — <see cref="TryReset"/> has no effect on it.
    /// </remarks>
    [PublicAPI]
    public bool TryReset()
    {
        while (true)
        {
            var state = Volatile.Read(ref _handshake);

            if ((state & Ready) == 0)
                return false;

            if (Interlocked.CompareExchange(ref _handshake, state & ~Ready, state) == state)
                return true;
        }
    }

    /// <summary>Raises readiness, waking an awaiting consumer if one is registered.</summary>
    /// <remarks>May be called from many threads. The wake (SetResult) is delivered after the CAS claim,
    /// so a synchronous continuation can safely re-enter <see cref="WaitAsync"/>.</remarks>
    [PublicAPI]
    public void Signal()
    {
        while (true)
        {
            var state = Volatile.Read(ref _handshake);

            // A waiter is registered: claim the Wait bit and deliver exactly one wake. Only one
            // signaler can win this CAS, so one registration is completed at most once.
            if ((state & Wait) != 0)
            {
                if (Interlocked.CompareExchange(ref _handshake, state & ~Wait, state) == state)
                    _core.SetResult(!Volatile.Read(ref _completed));
                return;
            }

            // No waiter: raise the readiness flag (if not already raised), to be consumed by the next
            // WaitAsync fast path or TryReset.
            if ((state & Ready) != 0)
                return;

            if (Interlocked.CompareExchange(ref _handshake, state | Ready, state) == state)
                return;
        }
    }

    /// <summary>
    /// Latches the terminal completion flag, making every subsequent <see cref="WaitAsync"/> return
    /// <see langword="false"/>. Wakes a waiter that is already registered (via the single-channel
    /// delivery of <see cref="Signal"/>, which completes with <see langword="false"/> since the flag
    /// is set).
    /// </summary>
    [PublicAPI]
    public void Complete()
    {
        if (Volatile.Read(ref _completed))
            return;

        Volatile.Write(ref _completed, true);

        // Wake a parked waiter immediately: Signal routes through a single delivery channel — if a
        // waiter is registered it is completed with false; otherwise the flag is raised and the next
        // WaitAsync returns false directly. Without this, a consumer that re-registered a pending wait
        // just before completion would sleep forever (no further Signal ever arrives).
        Signal();
    }

    /// <summary>Gets the status of the awaited operation.</summary>
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _core.GetStatus(token);

    /// <summary>Returns the result of the awaited operation.</summary>
    void IValueTaskSource.GetResult(short token)
        => _core.GetResult(token);

    /// <summary>Schedules the continuation to run when the operation completes.</summary>
    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    /// <summary>Gets the status of the awaited operation.</summary>
    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
        => _core.GetStatus(token);

    /// <summary>Returns the result of the awaited operation.</summary>
    bool IValueTaskSource<bool>.GetResult(short token)
        => _core.GetResult(token);

    /// <summary>Schedules the continuation to run when the operation completes.</summary>
    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}