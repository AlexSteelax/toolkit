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
/// The readiness core follows the same single-word CAS model as <see cref="CompleteSignal"/>: a
/// <c>_handshake</c> packs the handshake bits (currently only <c>Waiting</c>, since the source of
/// readiness is the <c>_readyMask</c> itself), and every transition is a bounded CAS loop. The
/// empty → non-empty transition of the mask (reported by the previous value returned by
/// <see cref="Interlocked.Or(ref uint, uint)"/> on the ready mask) acts as the readiness edge: either
/// it claims a registered wait and completes it exactly once, or it leaves the non-empty mask for the
/// next fast-path. <c>SetResult</c> is invoked outside any lock, so synchronous continuations may
/// safely re-enter <see cref="WaitAsync"/> without a deadlock.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class FanInSlim(bool allowSynchronousContinuations = true) : IValueTaskSource
{
    private const int Waiting = 0x1;

    // Readiness mask: the source of truth (non-empty ⟺ at least one event to consume).
    private uint _readyMask;

    // Handshake bits: Waiting = a waiter is registered on the current core version.
    private int _handshake;

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
        // Fast path: a ready slot is already pending — no registration needed.
        if (Volatile.Read(ref _readyMask) != 0)
            return ValueTask.CompletedTask;

        while (true)
        {
            // Re-arm the core to a fresh version only when we are about to register.
            _core.Reset();

            // A slot fired while we were re-arming: report it synchronously — the mask stays visible
            // for the caller's Take. We do not touch the core here; a Reset-per-round is harmless and
            // the next registration re-arms a fresh version.
            if (Volatile.Read(ref _readyMask) != 0)
                return ValueTask.CompletedTask;

            // Register as the waiter. Only one thread can win the Waiting bit, so exactly one
            // registration owns one core version.
            if (Interlocked.CompareExchange(ref _handshake, Waiting, 0) == 0)
            {
                // A first slot may have fired between the mask check above and the registration, after
                // the signaler already read the handshake without a waiter. Re-check the mask and, if
                // non-empty, release the wait and report it synchronously instead of parking.
                if (Volatile.Read(ref _readyMask) != 0 && Interlocked.CompareExchange(ref _handshake, 0, Waiting) == Waiting)
                    return ValueTask.CompletedTask;

                // Either the mask is still empty (we wait for a future Signal), or the signaler already
                // claimed the Waiting bit and will deliver SetResult for this fresh core.
                return new ValueTask(this, _core.Version);
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
        _ = Interlocked.And(ref _readyMask, ~ready);

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

        // Publish the event unconditionally: the mask is the source of truth, so the event is never
        // lost regardless of who is listening.
        var previous = Interlocked.Or(ref _readyMask, 1u << index);

        // Only the empty → non-empty transition of the mask acts as the readiness edge (like the
        // Ready bit in CompleteSignal). Further signals while the mask is already non-empty have
        // already been (or will be) observed by the active waiter.
        if (previous != 0)
            return;

        // The mask just became non-empty: if a waiter is registered, claim the Waiting bit and
        // deliver exactly one wake. Otherwise the non-empty mask is observed by the next fast path.
        if ((Volatile.Read(ref _handshake) & Waiting) != 0
            && Interlocked.CompareExchange(ref _handshake, 0, Waiting) == Waiting)
        {
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