# CompleteSignal

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`CompleteSignal.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)

A lightweight, **single-consumer readiness signal** backed by an `IValueTaskSource<bool>`: a producer raises [`Signal`](#signal), an awaiting consumer wakes via [`WaitAsync`](#waitasync), and the raised readiness signal is consumed via [`TryReset`](#tryreset) (edge-triggered). Completion is a separate terminal latch set by [`Complete`](#complete).

## Semantics

Readiness is a latch over a three-state machine: `0 = Idle, 1 = Waiting, 2 = Ready`.

- **`Signal()`** — **readiness**: a reusable edge-triggered state (`2`). Cleared by `TryReset` so the next write re-raises it. Wakes a registered waiter with `true`.
- **`Complete()`** — **completion**: latches a terminal flag (never cleared by `TryReset`) and makes every subsequent `WaitAsync` return `false`. It does not wake a waiter by itself — combine with `Signal()` to complete a registered wait (the waiter observes the terminal flag and gets `false`).

Because completion is irreversible, a later `Signal()` still wakes the waiter, but it yields `false` (the terminal flag wins).

## Characteristics

- **Edge-triggered** — the readiness signal is consumed (`TryReset`) only on a raised state; a signal raised between a failed check and a reset is never lost.
- **Lost-wakeup safe** — a stale readiness signal (raised while no work is actually available) is cleared and the wait is re-registered, closing the lost-wakeup window.
- **Terminal one-shot** — `Complete()` latches completion; it cannot be reset and always yields `false` from `WaitAsync`.
- **Single waiter** — designed for one awaiting consumer at a time; the `ValueTask<bool>` returned by `WaitAsync` must be awaited only once.
- **Synchronous completion** — `WaitAsync` completes synchronously with `true`/`false` when a signal is already pending.
- **Continuation control** — `allowSynchronousContinuations` (default `true`) inlines continuations on the signalling thread, or schedules them asynchronously when `false`.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var signal = new CompleteSignal();

// Producer:
signal.Signal();              // readiness (reusable)
// On stream end:
signal.Complete();            // latch completion
signal.Signal();              // wake the waiter → it observes completion (false)

// Consumer:
if (signal.TryReset())        // consumes a pending readiness signal without waiting
{
    // handle the ready state
}
else
{
    // wait: true = ready, false = completed
    if (!await signal.WaitAsync())
        return;               // the signal was completed
}
```

## API

### Constructor

`CompleteSignal(bool allowSynchronousContinuations = true)`

- `allowSynchronousContinuations` — when `true`, continuations run inline on the signalling thread; when `false`, they are scheduled asynchronously.

### Methods

| Member | Description |
|--------|-------------|
| `ValueTask<bool> WaitAsync()` | Waits for the signal to be raised without blocking the calling thread. Completes with `true` when readiness was signalled, or `false` when the signal was completed (terminal). Completes synchronously when a signal is already pending. |
| `bool TryReset()` | Consumes a raised readiness signal without waiting. Returns `true` if a readiness signal was pending (now cleared); otherwise `false`. Has no effect on a completed (terminal) signal. |
| `void Signal()` | Raises readiness, waking an awaiting consumer if one is registered. A registered waiter receives `true` unless the signal was completed, in which case it receives `false`. |
| `void Complete()` | Latches the terminal completion flag. Does not wake a waiter by itself; subsequent `WaitAsync` calls return `false`. |

## Notes

- `CompleteSignal` is the readiness core behind the await-based members of [`SpscChannel<T>`](SpscQueue.md), [`SpscChannelReader<T>`](SpscQueue.md) and [`SpscChannelWriter<T>`](SpscQueue.md): `Signal()` on readiness, `Complete()` + `Signal()` on stream completion.
- The `ValueTask<bool>` returned by `WaitAsync` is bound to an internal `IValueTaskSource<bool>` version token; await each returned `ValueTask<bool>` only once.

## See also

- [`SpscQueue<T>`](SpscQueue.md) — the SPSC queue family that uses `CompleteSignal` for read/write readiness (`WaitToReadAsync`/`WaitToWriteAsync` return `ValueTask<bool>`).
- [`FanInSlim`](FanInSlim.md) — a fan-in signal with similar wait/reset semantics over up to 32 slots.
