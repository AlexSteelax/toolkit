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
/// Readiness is a latch over a three-state machine: <c>0 = Idle, 1 = Waiting, 2 = Ready</c>.
/// <see cref="Signal"/> marks readiness (a reusable edge-triggered state), while <see cref="Complete"/>
/// latches a terminal completion flag that is never cleared by <see cref="TryReset"/> and makes every
/// subsequent <see cref="WaitAsync"/> return <see langword="false"/>.
/// </para>
/// <para>
/// A stale readiness signal (raised while no work is actually available) is cleared on <see cref="TryReset"/>
/// and a fresh wait is re-registered, so a signal raised between a failed check and a reset is never lost.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class CompleteSignal(bool allowSynchronousContinuations = true) : IValueTaskSource, IValueTaskSource<bool>
{
    // 0 = Idle, 1 = Waiting, 2 = Signaled
    private int _state;

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
    /// Completes synchronously when a signal is already pending, without consuming it — consumption is
    /// done separately via <see cref="TryReset"/>. Await each returned <see cref="ValueTask{TResult}"/>
    /// only once. A completed (terminal) signal always yields <see langword="false"/>.
    /// </remarks>
    [PublicAPI]
    public ValueTask<bool> WaitAsync()
    {
        // Completed (terminal): always report completion.
        if (Volatile.Read(ref _completed))
            return ValueTask.FromResult(false);

        while (true)
        {
            switch (Volatile.Read(ref _state))
            {
                // A readiness signal is raised: complete synchronously with true, without consuming it.
                // Consumption is the caller's responsibility (via TryReset), so a raised signal is never
                // silently lost.
                case 2:
                    return ValueTask.FromResult(!_completed);

                // Idle: rearm the core to a fresh version and register this wait. Only the thread that
                // transitions 0 → 1 wins the right to register; a concurrent signal (0 → 2, 1 → 2) or
                // completion cannot be lost.
                case 0:
                    _core.Reset();

                    if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
                        return new ValueTask<bool>(this, _core.Version);

                    continue;

                // case 1: a wait is already registered (a duplicate WaitAsync call). There is nothing
                // to (re)arm — the existing registration will be signaled by a producer.
                default:
                    return ValueTask.FromResult(!_completed);
            }
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
        while (Volatile.Read(ref _state) == 2)
        {
            if (Interlocked.CompareExchange(ref _state, 0, 2) == 2)
                return true;
        }

        return false;
    }

    /// <summary>Raises readiness, waking an awaiting consumer if one is registered.</summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal()
    {
        var current = Volatile.Read(ref _state);

        // Already ready — nothing to do.
        if (current == 2)
            return;

        // Idle → Ready without SetResult: no waiter is registered, so the charge is merely a "ready"
        // flag. The consumer observes 2 in WaitAsync/TryReset and returns immediately.
        if (current == 0)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) != 0)
                current = Volatile.Read(ref _state);
        }

        // Waiting → Ready: wake the registered waiter. SetResult is invoked outside any lock, so a
        // synchronous continuation may safely re-enter WaitAsync.
        if (current == 1)
        {
            if (Interlocked.CompareExchange(ref _state, 2, 1) == 1)
                _core.SetResult(!_completed);
        }
    }

    /// <summary>
    /// Latches the terminal completion flag, making every subsequent <see cref="WaitAsync"/> return
    /// <see langword="false"/>. Does not wake a waiter by itself — combine with <see cref="Signal"/>
    /// (or rely on the consumer re-checking) to complete a registered wait.
    /// </summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete()
    {
        Volatile.Write(ref _completed, true);
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
