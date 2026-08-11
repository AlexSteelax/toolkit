# BitTaskAny

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`BitTaskAny.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/BitTaskAny.cs)

A **bounded, lock-free** set of task slots that raises a signal when **any** tracked task completes.

## Characteristics

- **Edge-triggered signal** — raised exactly on the transition from "no completed tasks" to "at least one" (mirroring [`FanInSlim`](FanInSlim.md)).
- **Single-consumer model** — slot allocation and consumption happen on a single consumer thread; task completion may occur on any thread.
- **No lost completions** — a task that completes before `Insert` still triggers the signal synchronously.
- **32-bit capacity** — up to `MaxCapacity` (32) tracked slots.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var wakeUp = new TaskCompletionSource();
var set = new BitTaskAny(() => wakeUp.TrySetResult(), capacity: 8);

// Consumer thread: register started tasks while slots are free.
while (set.CanAdd)
{
    var slot = set.Insert(task);  // BitTaskAny.NoSlot when the set is full
    // ... schedule more work if a slot was allocated
}

// On the signal (0→1 transition), drain every ready task:
while (set.TryTake(out var index, out var completed))
{
    var result = completed.GetAwaiter().GetResult(); // rethrows on fault/cancel
}
```

## API

### Constants

| Member | Type | Description |
|--------|------|-------------|
| `MaxCapacity` | `int` | Maximum number of tracked task slots (`32`). |
| `NoSlot` | `int` | Slot index returned when no slot is available (`-1`). |

### Constructor

`BitTaskAny(Action signal, int capacity = MaxCapacity)` — `signal` is invoked on the 0→1 transition; `capacity` must be in **1..MaxCapacity**. Throws `ArgumentNullException` / `ArgumentOutOfRangeException`.

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `Capacity` | `int` | The number of task slots. |
| `Count` | `int` | The number of currently occupied slots. |
| `CountReady` | `int` | The number of completed tasks currently available. |
| `HasReady` | `bool` | `true` when at least one completed task is available. |
| `CanAdd` | `bool` | `true` when at least one free slot is available for `Insert` (consumer thread). |

### Methods

| Method | Description |
|--------|-------------|
| `int Insert(Task task)` | Registers a task into a free slot and attaches the completion signal. Returns the allocated slot, or `NoSlot` when the set is full. Must be called from the consumer thread. |
| `bool TryTake(out int index, out Task task)` | Takes a completed task, freeing its slot for reuse. Returns `false` when none is ready. Must be called from the consumer thread; the returned task is guaranteed completed. |

## Example: full set behavior

```csharp
var set = new BitTaskAny(() => { }, capacity: 4);

for (var i = 0; i < 4; i++)
    Assert.NotEqual(BitTaskAny.NoSlot, set.Insert(Task.CompletedTask));

Assert.Equal(BitTaskAny.NoSlot, set.Insert(Task.CompletedTask)); // full
Assert.Equal(4, set.Count);
```

## See also

- [`FanInSlim`](FanInSlim.md)
- [`EventTask<T>`](EventTask.md) / [`EventEnumerator<T>`](EventEnumerator.md)
