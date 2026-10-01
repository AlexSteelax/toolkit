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
/// <see cref="Signal"/> and <see cref="Complete"/> to deliver exactly one wake.</item>
/// <item><c>0x4 (Completed)</c> — the terminal completion latch. Raised by <see cref="Complete"/>;
/// once latched, every subsequent <see cref="WaitAsync"/> returns <see langword="false"/> and
/// <see cref="Signal"/> is ignored.</item>
/// </list>
/// Delivery is single-channel: a signal either claims the <c>Wait</c> bit and calls
/// <c>SetResult</c>, or raises the <c>Ready</c> bit for a future fast path. Both can never be held at
/// once by the same event, so neither double-delivery nor lost-wakeup can occur. Because the waiter
/// registration CAS compares the whole word, it can never succeed once <c>Completed</c> is latched, and
/// a completion landing just after registration observes the <c>Wait</c> bit and delivers exactly one
/// wake — no separate completion flag read is needed.
/// </para>
/// <para>
/// <see cref="ManualResetValueTaskSourceCore{TResult}.SetResult"/> is always invoked outside the CAS
/// loop: a synchronous continuation (<c>allowSynchronousContinuations</c>) may re-enter
/// <see cref="WaitAsync"/> on the same thread.
/// </para>
/// <para>
/// <see cref="Complete"/> latches the terminal completion bit, making every subsequent
/// <see cref="WaitAsync"/> return <see langword="false"/>, and wakes a registered waiter immediately.
/// </para>
/// </remarks>
[PublicAPI]
public class CompleteSignal(bool allowSynchronousContinuations = true) : IValueTaskSource, IValueTaskSource<bool>
{
    private const int Ready = 0x1;
    private const int Wait = 0x2;
    private const int Completed = 0x4;

    // Packed handshake state: 0 = idle, Ready = event pending, Wait = waiter registered,
    // Completed = terminal completion latch (never cleared).
    private int _handshake;
    
    private ManualResetValueTaskSourceCore<bool> _core = new()
    {
        RunContinuationsAsynchronously = !allowSynchronousContinuations
    };

    /// <summary>
    /// Gets a value indicating whether the instance latched the terminal completion via
    /// <see cref="Complete"/>.
    /// </summary>
    [PublicAPI]
    public virtual bool IsCompleted
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Volatile.Read(ref _handshake) & Completed) != 0;
    }
    
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual ValueTask<bool> WaitAsync()
    {
        while (true)
        {
            var state = Volatile.Read(ref _handshake);

            // Completed (terminal): always report completion, even if a readiness marker is pending.
            if ((state & Completed) != 0)
                return ValueTask.FromResult(false);

            // A readiness event is pending: consume it synchronously and report it, without touching
            // the core. This is the same semantic path both on entry and when a signal lands while we
            // are re-arming — no need for a "virtual" pending task. The CAS proves the word held no
            // Completed bit at the linearization point, so the result is deterministic. A derived
            // type may back the edge with a separately tracked payload and must not report a charge
            // whose payload is already gone — checked via <see cref="HasPendingReadiness"/>.
            if ((state & Ready) != 0)
            {
                if (Interlocked.CompareExchange(ref _handshake, state & ~Ready, state) != state)
                    continue;

                if (!HasPendingReadiness())
                    continue;

                return ValueTask.FromResult(true);
            }

            // Register as the waiter. Re-arm the core to a fresh version just before claiming the
            // Wait bit: only this thread owns the core, so there is no competing Reset. The CAS
            // compares the whole word, so it cannot succeed once Completed is latched; a completion
            // that lands just after the CAS observes the Wait bit we own and delivers SetResult(false)
            // for this fresh core version. A Signal that lands between the state read and this CAS
            // either loses the CAS (we regain the loop and observe Ready) or, if the Wait bit wins,
            // claims the Wait bit and delivers SetResult.
            _core.Reset();

            if (Interlocked.CompareExchange(ref _handshake, state | Wait, state) == state)
                return new ValueTask<bool>(this, _core.Version);
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual ValueTask WaitToReadyAsync()
    {
        while (true)
        {
            var state = Volatile.Read(ref _handshake);

            // Completed (terminal): always report completion, even if a readiness marker is pending.
            if ((state & Completed) != 0)
                return ValueTask.CompletedTask;

            // A readiness event is pending: consume it synchronously and report it, without touching
            // the core. This is the same semantic path both on entry and when a signal lands while we
            // are re-arming — no need for a "virtual" pending task. The CAS proves the word held no
            // Completed bit at the linearization point, so the result is deterministic. A derived
            // type may back the edge with a separately tracked payload and must not report a charge
            // whose payload is already gone — checked via <see cref="HasPendingReadiness"/>.
            if ((state & Ready) != 0)
            {
                if (Interlocked.CompareExchange(ref _handshake, state & ~Ready, state) != state)
                    continue;

                if (!HasPendingReadiness())
                    continue;

                return ValueTask.CompletedTask;
            }

            // Register as the waiter. Re-arm the core to a fresh version just before claiming the
            // Wait bit: only this thread owns the core, so there is no competing Reset. The CAS
            // compares the whole word, so it cannot succeed once Completed is latched; a completion
            // that lands just after the CAS observes the Wait bit we own and delivers SetResult(false)
            // for this fresh core version. A Signal that lands between the state read and this CAS
            // either loses the CAS (we regain the loop and observe Ready) or, if the Wait bit wins,
            // claims the Wait bit and delivers SetResult.
            _core.Reset();

            if (Interlocked.CompareExchange(ref _handshake, state | Wait, state) == state)
                return new ValueTask(this, _core.Version);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool TryReset()
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
    /// so a synchronous continuation can safely re-enter <see cref="WaitAsync"/>. Has no effect once
    /// the signal is completed via <see cref="Complete"/>.</remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Signal()
    {
        while (true)
        {
            var state = Volatile.Read(ref _handshake);

            // Completed (terminal): further readiness is meaningless.
            if ((state & Completed) != 0)
                return;

            // A waiter is registered: claim the Wait bit and deliver exactly one wake. Only one
            // signaler can win this CAS, so one registration is completed at most once.
            if ((state & Wait) != 0)
            {
                if (Interlocked.CompareExchange(ref _handshake, state & ~Wait, state) == state)
                {
                    _core.SetResult(true);
                    return;
                }

                continue;
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
    /// Latches the terminal completion bit, making every subsequent <see cref="WaitAsync"/> return
    /// <see langword="false"/>. Wakes a waiter that is already registered.
    /// </summary>
    /// <remarks>
    /// Idempotent: only the first invocation latches the bit and wakes a waiter. A waiter that
    /// registers after the latch observes the completed bit on its next <see cref="WaitAsync"/> pass
    /// and completes with <see langword="false"/> without parking. Signals issued afterwards are
    /// ignored.
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Complete()
    {
        // Atomically latch the Completed bit; the previous value reveals both whether completion was
        // already latched and whether a waiter is registered at this instant.
        var previous = Interlocked.Or(ref _handshake, Completed);

        if ((previous & Completed) != 0)
            return;

        // A waiter is registered: claim the Wait bit and deliver exactly one wake. Only one party can
        // win the CAS, so one registration is completed at most once. If no waiter is registered, the
        // latched Completed bit is observed by the next WaitAsync fast path.
        if ((previous & Wait) != 0
            && Interlocked.CompareExchange(ref _handshake, (previous | Completed) & ~Wait, previous | Completed) == (previous | Completed))
        {
            _core.SetResult(false);
        }
    }
    
    /// <summary>
    /// Determines whether a charged <see cref="Ready"/> edge still corresponds to a reportable event.
    /// </summary>
    /// <remarks>
    /// The base implementation always returns <see langword="true"/>. A derived type that backs the
    /// edge with a separately tracked payload (e.g. <see cref="FanInSlim"/> and its slot mask) should
    /// override this to return the presence of such payload: a charge whose payload was already drained
    /// is stale and must not produce a spurious synchronous "ready" report.
    /// </remarks>
    protected virtual bool HasPendingReadiness()
    {
        return true;
    }

    #region IValueTaskSource
    
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        => _core.GetStatus(token);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IValueTaskSource.GetResult(short token)
        => _core.GetResult(token);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
        => _core.GetStatus(token);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IValueTaskSource<bool>.GetResult(short token)
        => _core.GetResult(token);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
    
    #endregion
}