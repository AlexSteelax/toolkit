# FanInSlim

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`FanInSlim.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/FanInSlim.cs)

A lightweight **fan-in signal** that aggregates multiple asynchronous sources into a single awaitable, reporting **which of up to 32 slots** have fired.

## Characteristics

- **Not thread-safe for consumers** — all public methods must be called from a single thread, though task completion (signaling) may occur on any thread.
- **Synchronous completion** — `WaitAsync` completes synchronously if a signal is already pending.
- **Consumer-managed lifecycle** — the consumer is responsible for source lifecycle and re-registration.
- **Bitmask of ready slots** — fired slots are obtained via `Take()`, which returns a raw `uint` bitmask (inspect it with [`AdvanceBitOperations`](AdvanceBitOperations.md)).

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;
using Steelax.Toolkit.HighPerformance.Primitives;

var fanIn = new FanInSlim();

// Producers signal slots from any thread:
fanIn.Signal(0);
fanIn.Signal(2);

// Consumer waits until at least one slot fired:
await fanIn.WaitAsync();          // completes synchronously here

var ready = fanIn.Take();         // uint mask 0b101 { 0, 2 } — gets and clears ready slots
if (AdvanceBitOperations.IsSet(ready, 0))
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
| `uint Take()` | Gets and clears the set of ready slots without waiting (concurrent completions are preserved). Returns `0` when nothing is pending. Remains usable after `Complete()` to drain leftovers. |
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

AdvanceBitOperations.IsSet(fanIn.Take(), 2); // true — leftovers before completion are still drainable
```

## Example: signal-before-wait and signal-after-wait

```csharp
var source = new FanInSlim();

// Signal before wait — synchronous completion:
source.Signal(0);
source.Signal(2);
source.Signal(4);
await source.WaitAsync();
Assert.True(AdvanceBitOperations.HasAny(source.Take()));

// Signal after wait — asynchronous completion:
var waitTask = source.WaitAsync();
Assert.False(waitTask.IsCompleted);

source.Signal(1);
await waitTask;
Assert.True(AdvanceBitOperations.HasAny(source.Take()));
```

## Example: all 32 slots

```csharp
var source = new FanInSlim();

for (var i = 0; i < 32; i++)
    source.Signal(i);

await source.WaitAsync();
var slots = source.Take();

slots != 0;                              // any
BitOperations.PopCount(slots) == 32;     // all 32
```

## See also

- [`FanInSignalCallback`](FanInSignalCallback.md) — allocation-free signal handle for a slot.
- [`AdvanceBitOperations`](AdvanceBitOperations.md) — the functions to build and inspect the raw `uint` mask returned by `Take()`.
- [`EventEnumerator<T>`](EventEnumerator.md) / [`EventTask<T>`](EventTask.md) — sources that signal fan-in slots.
