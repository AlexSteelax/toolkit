# SlotSet

> Namespace: `Steelax.Toolkit.HighPerformance.Primitives`
> Source: [`SlotSet.cs`](../src/Steelax.Toolkit.HighPerformance/Primitives/SlotSet.cs)

A set of slot indices (**0..31**) represented as a 32-bit mask.

## Characteristics

- **Bitmask representation** — each set bit (0..31) is a slot index; efficient `PopCount`-based `Count`.
- **Immutable operations** — all operations return a **new instance**; the original is never mutated.
- **Value equality** — provided by the compiler (it's a `readonly record struct`).

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Primitives;

var set = SlotSet.Of(0, 2, 5);

set.Any;              // true
set.Count;            // 3
set.IsSet(2);         // true

var rest = set.Pop(out var first); // first = 0 (lowest set slot removed)
rest.Mask;                          // raw bitmask of the remaining slots

var fromMask = SlotSet.FromMask(0b1101u); // slots 0, 2, 3
```

## API

### Constants

| Member | Type | Description |
|--------|------|-------------|
| `None` | `int` | Sentinel index (`-1`) returned by `Pop` when no slots are set. |

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `Mask` | `uint` | The raw bitmask value. |
| `Any` | `bool` | `true` when at least one slot is set. |
| `Count` | `int` | The number of set slots (`BitOperations.PopCount`). |

### Static factories

| Method | Description |
|--------|-------------|
| `FromMask(uint mask)` | Creates a set from a raw bitmask. |
| `Of(params int[] slots)` | Creates a set from slot indices; throws `ArgumentOutOfRangeException` for indices outside 0..31. |

### Methods

| Method | Description |
|--------|-------------|
| `IsSet(int index)` | `true` when the slot is set; throws for indices outside 0..31. |
| `Pop(out int index)` | Removes and returns the **lowest-indexed** set slot (`None` when empty); returns the remaining set. |
| `Remove(int index, out bool original)` | Removes a slot; `original` is `true` when the slot was present. |
| `ToString()` | Raw mask followed by set indices, e.g. `"11[0 1 3]"`. |

## Example: consuming slots one by one

```csharp
var set = SlotSet.Of(1, 3, 7);

while (set.Any)
{
    set = set.Pop(out var index);
    Console.WriteLine($"handled slot {index}"); // 1, 3, 7
}
```

## See also

- [`FanInSlim`](FanInSlim.md) — `Take()` returns a `SlotSet` of fired slots.
- [`Deque<T>`](Deque.md)
