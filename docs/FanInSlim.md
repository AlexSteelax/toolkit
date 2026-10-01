# FanInSlim

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`FanInSlim.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/FanInSlim.cs)

A lightweight **fan-in signal** that aggregates multiple asynchronous sources into a single awaitable, reporting **which of up to 32 slots** have fired.

## Characteristics

- **Not thread-safe for consumers** — all public methods must be called from a single thread, though task completion (signaling) may occur on any thread.
- **Synchronous completion** — `WaitAsync` completes synchronously if a signal is already pending.
- **Consumer-managed lifecycle** — the consumer is responsible for source lifecycle and re-registration.
- **Bitmask of ready slots** — fired slots are obtained via `Take()`, which returns a [`SlotSet`](SlotSet.md).

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var fanIn = new FanInSlim();

// Producers signal slots from any thread:
fanIn.Signal(0);
fanIn.Signal(2);

// Consumer waits until at least one slot fired:
await fanIn.WaitAsync();          // completes synchronously here

var ready = fanIn.Take();         // SlotSet { 0, 2 } — gets and clears ready slots
if (ready.IsSet(0))
{
    // handle source 0
}

// Re-arm an individual slot for the next round:
fanIn.TryReset(0);
```

## API

### Methods

| Method | Description |
|--------|-------------|
| `ValueTask WaitAsync()` | Waits until at least one slot signals readiness or the instance is completed. Returns synchronously when a signal is already pending. Fired slots are obtained via `Take()`. |
| `ValueTask<bool> WaitToReadyAsync()` | Like `WaitAsync`, but distinguishes the outcome: `true` when a slot fired, `false` when the instance was completed. Completion dominates — even un-drained slots signalled before `Complete()` do not prevent `false`. |
| `SlotSet Take()` | Gets and clears the set of ready slots without waiting (concurrent completions are preserved). Remains usable after `Complete()` to drain leftovers. |
| `bool TryReset(int index)` | Resets the ready flag of the specified slot, if it was set. Returns `true` if the slot was ready and has been reset. |
| `void Signal(int index)` | Marks the specified slot as ready, waking the awaiting consumer if it was idle. Ignored once the instance is completed. |
| `void Complete()` | Latches the terminal completion latch: makes every subsequent `WaitToReadyAsync` (with an empty mask) return `false` and wakes a registered waiter immediately. Idempotent. |
| `bool IsCompleted` | Gets whether the terminal completion was latched via `Complete()`. |
| `FanInSignalCallback GetSignalCallback(int index)` | Creates a zero-allocation signal callback for the specified slot. |

All methods validate `index` in range **0..31** and throw `ArgumentOutOfRangeException` otherwise.

## Completion

```csharp
var fanIn = new FanInSlim();

someone.Signal(2);      // last slot before shutdown
fanIn.Complete();       // terminal: completion dominates, no further Signal() is accepted

await fanIn.WaitToReadyAsync();   // false — completed, even though slot 2 is still un-drained

fanIn.Take().IsSet(2);            // true — leftovers before completion are still drainable
```

## Example: signal-before-wait and signal-after-wait

```csharp
var source = new FanInSlim();

// Signal before wait — synchronous completion:
source.Signal(0);
source.Signal(2);
source.Signal(4);
await source.WaitAsync();
Assert.True(source.Take().Any);

// Signal after wait — asynchronous completion:
var waitTask = source.WaitAsync();
Assert.False(waitTask.IsCompleted);

source.Signal(1);
await waitTask;
Assert.True(source.Take().Any);
```

## Example: all 32 slots

```csharp
var source = new FanInSlim();

for (var i = 0; i < 32; i++)
    source.Signal(i);

await source.WaitAsync();
var slots = source.Take();

slots.Any;   // true
slots.Count; // 32
```

## See also

- [`FanInSignalCallback`](FanInSignalCallback.md) — allocation-free signal handle for a slot.
- [`SlotSet`](SlotSet.md) — the value returned by `Take()`.
- [`EventEnumerator<T>`](EventEnumerator.md) / [`EventTask<T>`](EventTask.md) — sources that signal fan-in slots.
