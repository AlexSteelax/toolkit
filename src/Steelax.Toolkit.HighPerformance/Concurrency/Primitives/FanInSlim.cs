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
/// The readiness handshake is an exact mirror of <see cref="CompleteSignal"/>: a single <c>int</c>
/// packs the <c>Ready</c>, <c>Wait</c> and <c>Completed</c> bits, and every transition is a bounded
/// CAS loop. The slot payload (<c>_readyMask</c>) is decoupled: it never decides whether to wait — it
/// is consulted only on the hot path (to confirm that a charged <c>Ready</c> edge still has an
/// un-drained payload) and is drained by <see cref="Take"/>. Each signal publishes its slot into the
/// mask (unconditionally, so it is never lost) and feeds the single-channel handshake exactly like
/// <see cref="CompleteSignal.Signal"/>: it either claims a registered <c>Wait</c> and delivers
/// exactly one wake, or charges the <c>Ready</c> bit for the next fast path. <c>SetResult</c> is
/// invoked outside any CAS, so synchronous continuations may safely re-enter <see cref="WaitToReadyAsync"/>
/// without a deadlock.
/// </para>
/// <para>
/// Because the edge is charged into the handshake word, the waiter registration CAS
/// (<c>state | Wait</c>) observes a racing signal as a word change and re-loops — there is no
/// separate post-registration re-check, exactly like <see cref="CompleteSignal"/>.
/// </para>
/// <para>
/// <see cref="CompleteSignal.Complete"/> latches the terminal completion bit. After completion, further
/// <see cref="Signal"/> calls are ignored, while <see cref="Take"/> and <see cref="TryTake"/> remain
/// usable so any slots that were signalled before the completion can still be drained.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class FanInSlim(bool allowSynchronousContinuations = true) : CompleteSignal(allowSynchronousContinuations)
{
    // Readiness mask: the slot payload. Non-empty ⟺ at least one event to consume. Consulted only on
    // the hot path and drained by Take; it never drives the wait/registration decision.
    private uint _readyMask;

    /// <inheritdoc/>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask WaitToReadyAsync()
    {
        return base.WaitToReadyAsync();
    }

    /// <inheritdoc/>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override ValueTask<bool> WaitAsync()
    {
        return base.WaitAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This flag stays set once latched. It does not reflect the state of <see cref="_readyMask"/>:
    /// slots signalled before completion can still be drained via <see cref="Take"/>.
    /// </remarks>
    [PublicAPI]
    public override bool IsCompleted
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => base.IsCompleted;
    }

    /// <summary>Gets and clears the set of ready slots without waiting.</summary>
    /// <returns>
    /// A <see cref="SlotSet"/> of the slots fired since the last take, or an empty set.
    /// </returns>
    /// <remarks>
    /// Remains usable after <see cref="CompleteSignal.Complete"/> — any slots that were signalled before completion
    /// can still be drained.
    /// </remarks>
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public SlotSet Peek()
    {
        return new SlotSet(Volatile.Read(ref _readyMask));
    }

    /// <summary>Resets the ready flag of the specified slot, if it was set.</summary>
    /// <param name="index">The slot index (0..31) to reset.</param>
    /// <returns>
    /// <see langword="true"/> if the slot was ready and has been reset;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    public bool TryTake(int index)
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
    /// <remarks>
    /// Has no effect once the instance is completed via <see cref="CompleteSignal.Complete"/>.
    /// May be called from many threads. The wake (SetResult) is delivered after the CAS claim,
    /// so a synchronous continuation can safely re-enter <see cref="WaitToReadyAsync"/>.
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, sizeof(int) * 8);

        // Completion was latched: further signals are meaningless and must not even touch the payload.
        if (IsCompleted)
            return;

        // Publish the slot into the payload mask unconditionally: the mask is never lost regardless
        // of who is listening — the consumer drains it via Take.
        _ = Interlocked.Or(ref _readyMask, 1u << index);

        base.Signal();
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
    
    /// <remarks>
    /// A charged <see cref="CompleteSignal"/> edge is reportable only while it still has an un-drained
    /// slot payload; edges left over after <see cref="Take"/> (stale raises) are suppressed, so a wait
    /// never completes with an empty mask.
    /// </remarks>
    protected override bool HasPendingReadiness()
    {
        return Volatile.Read(ref _readyMask) != 0;
    }
}
