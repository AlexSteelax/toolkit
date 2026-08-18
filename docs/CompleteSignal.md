# CompleteSignal

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`CompleteSignal.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)

A lightweight, **single-consumer readiness signal** backed by an `IValueTaskSource`: a producer raises [`Signal`](#signal), an awaiting consumer wakes via [`WaitAsync`](#waitasync), and the raised signal is consumed via [`TryReset`](#tryreset) (edge-triggered).

## Characteristics

- **Edge-triggered** — the signal is consumed (`TryReset`) only on a raised state; a signal raised between a failed check and a reset is never lost.
- **Lost-wakeup safe** — a stale signal (raised while no work is actually available) is cleared and the wait is re-registered, closing the lost-wakeup window.
- **Single waiter** — designed for one awaiting consumer at a time; the `ValueTask` returned by `WaitAsync` must be awaited only once.
- **Synchronous completion** — `WaitAsync` completes synchronously when a signal is already pending.
- **Continuation control** — `allowSynchronousContinuations` (default `true`) inlines continuations on the signalling thread, or schedules them asynchronously when `false`.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var signal = new CompleteSignal();

// Producer:
signal.Signal();          // raises the signal, waking a registered waiter

// Consumer:
if (signal.TryReset())    // consumes a pending signal without waiting
{
    // handle the ready state
}
else
{
    await signal.WaitAsync();   // wait for the next raise
}
```

## API

### Constructor

`CompleteSignal(bool allowSynchronousContinuations = true)`

- `allowSynchronousContinuations` — when `true`, continuations run inline on the signalling thread; when `false`, they are scheduled asynchronously.

### Methods

| Member | Description |
|--------|-------------|
| `ValueTask WaitAsync()` | Waits for the signal to be raised without blocking the calling thread. Completes synchronously when a signal is already pending. |
| `bool TryReset()` | Consumes the raised signal without waiting. Returns `true` if a signal was pending (now cleared); otherwise `false`. |
| `void Signal()` | Raises the signal, waking an awaiting consumer if one is registered. |

## Notes

- `CompleteSignal` is the readiness core behind the await-based members of [`SpscChannel<T>`](SpscQueue.md), [`SpscChannelReader<T>`](SpscQueue.md) and [`SpscChannelWriter<T>`](SpscQueue.md).
- The `ValueTask` returned by `WaitAsync` is bound to an internal `IValueTaskSource` version token; await each returned `ValueTask` only once.

## See also

- [`SpscQueue<T>`](SpscQueue.md) — the SPSC queue family that uses `CompleteSignal` for read/write readiness.
- [`FanInSlim`](FanInSlim.md) — a fan-in signal with similar wait/reset semantics over up to 32 slots.
