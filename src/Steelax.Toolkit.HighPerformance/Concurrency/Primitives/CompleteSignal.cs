using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// A single-consumer readiness signal backed by an <see cref="IValueTaskSource"/>: a producer raises
/// <see cref="Signal"/>, an awaiting consumer wakes via <see cref="WaitAsync"/>, and the raised signal
/// is consumed via <see cref="TryReset"/> (edge-triggered).
/// </summary>
/// <remarks>
/// Readiness is a latch over a <c>0 = Idle, 1 = Waiting, 2 = Signaled</c> state machine. A stale signal
/// (raised while no work is actually available) is cleared on <see cref="TryReset"/> and a fresh wait is
/// re-registered, so a signal raised between a failed check and a reset is never lost.
/// </remarks>
[PublicAPI]
public sealed class CompleteSignal(bool allowSynchronousContinuations = true) : IValueTaskSource
{
    // 0 = Idle, 1 = Waiting, 2 = Signaled
    private int _state;

    private ManualResetValueTaskSourceCore<bool> _core = new()
    {
        RunContinuationsAsynchronously = !allowSynchronousContinuations
    };

    /// <summary>
    /// Waits for the signal to be raised without blocking the calling thread.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that completes when the signal is raised.</returns>
    /// <remarks>
    /// Completes synchronously when a signal is already pending, without consuming it — consumption is
    /// done separately via <see cref="TryReset"/>. Await each returned <see cref="ValueTask"/> only once.
    /// </remarks>
    [PublicAPI]
    public ValueTask WaitAsync()
    {
        while (true)
        {
            switch (Volatile.Read(ref _state))
            {
                // A signal is raised: complete synchronously without consuming it. Consumption is the
                // caller's responsibility (via TryReset), so a raised signal is never silently lost.
                case 2:
                    return ValueTask.CompletedTask;

                // Idle: rearm the core to a fresh version and register this wait. Only the thread that
                // transitions 0 → 1 wins the right to register; a concurrent signal (0 → 2 / 1 → 2)
                // cannot be lost.
                case 0:
                    _core.Reset();

                    if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
                        return new ValueTask(this, _core.Version);

                    continue;

                // case 1: a wait is already registered (a duplicate WaitAsync call). There is nothing
                // to (re)arm — the existing registration will be signaled by a producer.
                default:
                    return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Consumes the raised signal without waiting.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a signal was pending (now cleared); otherwise <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    public bool TryReset()
    {
        while (Volatile.Read(ref _state) == 2)
        {
            if (Interlocked.CompareExchange(ref _state, 0, 2) == 2)
                return true;
        }

        return false;
    }

    /// <summary>Raises the signal, waking an awaiting consumer if one is registered.</summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal()
    {
        var current = Volatile.Read(ref _state);

        // Already signaled — nothing to do.
        if (current == 2)
            return;

        // Idle → Signaled without SetResult: no waiter is registered, so the charge is merely a
        // "signal is raised" flag. The consumer observes 2 in WaitAsync/TryReset and returns immediately.
        if (current == 0)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) != 0)
                current = Volatile.Read(ref _state);
        }

        // Waiting → Signaled: wake the registered waiter. SetResult is invoked outside any lock, so a
        // synchronous continuation may safely re-enter WaitAsync.
        if (current == 1)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 1) == 1)
                _core.SetResult(true);
        }
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
}
