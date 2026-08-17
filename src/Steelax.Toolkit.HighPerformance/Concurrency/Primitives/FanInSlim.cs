using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// A lightweight fan-in signal that aggregates multiple asynchronous sources into a
/// single awaitable, reporting which of up to 32 slots have fired.
/// </summary>
/// <remarks>
/// <para>
/// This type is NOT thread-safe for consumers: all public methods must be called from
/// a single thread, though task completion may occur on any thread.
/// </para>
/// <para>
/// The consumer is responsible for managing source lifecycle and re-registration.
/// </para>
/// <para>
/// The readiness core is driven by a lock-free state machine (<c>0</c> = idle, <c>1</c> = waiting,
/// <c>2</c> = signaled) with CAS transitions. <c>SetResult</c> is invoked outside any lock, so
/// synchronous continuations may safely re-enter <see cref="WaitAsync"/> without a deadlock.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class FanInSlim(bool allowSynchronousContinuations = true) : IValueTaskSource
{
    private uint _readyMask;

    // 0 = Idle, 1 = Waiting, 2 = Signaled
    private int _state;

    private ManualResetValueTaskSourceCore<object?> _core = new()
    {
        RunContinuationsAsynchronously = !allowSynchronousContinuations
    };

    /// <summary>
    /// Waits until at least one slot signals readiness.
    /// </summary>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes when a slot fires;
    /// the fired slots are then obtained via <see cref="Take"/>.
    /// </returns>
    /// <remarks>
    /// Returns synchronously if a signal is already pending.
    /// </remarks>
    [PublicAPI]
    public ValueTask WaitAsync()
    {
        if (Volatile.Read(ref _readyMask) != 0)
            return ValueTask.CompletedTask;

        while (true)
        {
            switch (Volatile.Read(ref _state))
            {
                // A signal is raised but no ready slots remain (they were taken): clear the stale
                // signal (2 → 0) and fall through to register a fresh wait. A signal raised in the
                // meantime is not lost — the loop re-reads _state.
                case 2:
                    Interlocked.CompareExchange(ref _state, 0, 2);
                    continue;

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

    /// <summary>Gets and clears the set of ready slots without waiting.</summary>
    /// <returns>
    /// A <see cref="SlotSet"/> of the slots fired since the last take, or an empty set.
    /// </returns>
    [PublicAPI]
    public SlotSet Take()
    {
        var ready = Volatile.Read(ref _readyMask);

        if (ready == 0)
            return new SlotSet();

        // Only clear the bits being returned; concurrent completions are preserved.
        Interlocked.And(ref _readyMask, ~ready);

        return new SlotSet(ready);
    }

    /// <summary>Resets the ready flag of the specified slot, if it was set.</summary>
    /// <param name="index">The slot index (0..31) to reset.</param>
    /// <returns>
    /// <see langword="true"/> if the slot was ready and has been reset;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    public bool TryReset(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, sizeof(int) * 8);

        var bit = 1u << index;

        var origin = Interlocked.And(ref _readyMask, ~bit);

        return (origin & bit) != 0;
    }

    /// <summary>
    /// Marks the specified slot as ready, waking the awaiting consumer if it was idle.
    /// </summary>
    /// <param name="index">The slot index (0..31) to signal.</param>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, sizeof(int) * 8);

        _ = Interlocked.Or(ref _readyMask, 1u << index);

        var current = Volatile.Read(ref _state);

        // Already signaled — nothing to do.
        if (current == 2)
            return;

        // Idle → Signaled without SetResult: no waiter is registered, so the charge is merely a
        // "signal is raised" flag. The consumer observes 2 in WaitAsync and returns immediately.
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
                _core.SetResult(null);
        }
    }

    /// <summary>Creates a zero-allocation signal callback for the specified slot.</summary>
    /// <param name="index">The slot index (0..31) to signal.</param>
    [PublicAPI]
    public FanInSignalCallback GetSignalCallback(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, sizeof(int) * 8);

        return new FanInSignalCallback(this, index);
    }

    /// <summary>Gets the status of the current operation.</summary>
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _core.GetStatus(token);

    /// <summary>Completes the awaited operation; the mask is consumed via <see cref="Take"/>.</summary>
    void IValueTaskSource.GetResult(short token)
        => _core.GetResult(token);

    /// <summary>Schedules the continuation for the awaiting consumer.</summary>
    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}
