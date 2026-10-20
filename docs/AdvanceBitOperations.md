# AdvanceBitOperations

> Namespace: `Steelax.Toolkit.HighPerformance`
> Source: [`AdvanceBitOperations.cs`](../src/Steelax.Toolkit.HighPerformance/AdvanceBitOperations.cs)

Functional bit-mask operations over the **32 slot indices (0..31)** packed into a `uint`.

## Characteristics

- **Plain `uint` masks** — a slot mask is a raw `uint` where each set bit is a slot index.
- **Pure functions** — masks are passed by value; stateful transitions (`TryPop`, `TrySet`, `TryUnset`) report their result through `out` parameters.
- **Native composition** — combining masks (`|`, `&`, `^`, `~`, `!= 0`) is left to the built-in operators; this class only adds what they cannot express.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance;

var mask = 0b100101u; // slots 0, 2, 5

AdvanceBitOperations.HasAny(mask);   // true
AdvanceBitOperations.IsSet(mask, 2); // true

var removed = AdvanceBitOperations.TryPop(mask, out var first, out var rest); // first = 0, removed = true (lowest set slot removed)
```

## API

All methods are `static`. Slot indices are validated in **0..31** (ranges in **0..32**) and throw `ArgumentOutOfRangeException` otherwise.

| Method | Description |
|--------|-------------|
| `uint OfFirstBits(int count)` | The first (lowest) `count` bits set (e.g. `OfFirstBits(3) == 0b111`). |
| `uint OfLastBits(int count)` | The last (highest) `count` bits set (complement of `OfFirstBits(32 - count)`). |
| `bool HasAny(uint mask)` | `true` when at least one slot is set. |
| `bool HasAll(uint mask)` | `true` when all 32 slots are set. |
| `bool IsSet(uint mask, int index)` | `true` when the slot is set. |
| `bool TryPop(uint mask, out int index, out uint rest)` | Removes the lowest-indexed set slot (hot path, no rotation). |
| `bool TryPop(uint mask, out int index, out uint rest, int offset)` | Same, but scans cyclically from `offset` (queue semantics: `offset` sets where collection starts). |
| `bool TryUnset(uint mask, int index, out uint rest)` | Removes a slot when it is set; `true` when it was present. |
| `void Unset(uint mask, int index, out uint rest)` | Removes a slot idempotently, the resulting mask is returned in `rest`. |
| `bool TrySet(uint mask, int index, out uint rest)` | Sets a slot when it is not already set; `true` when it was added. |
| `void Set(uint mask, int index, out uint rest)` | Sets a slot idempotently, the resulting mask is returned in `rest`. |

## Example: consuming slots one by one

```csharp
var mask = 0b10001010u; // slots 1, 3, 7

while (AdvanceBitOperations.TryPop(mask, out var index, out mask))
{
    Console.WriteLine($"handled slot {index}"); // 1, 3, 7
}
```

## Example: rotating through slots (queue start)

```csharp
var mask = AdvanceBitOperations.OfFirstBits(4); // slots 0..3

while (AdvanceBitOperations.TryPop(mask, out var index, out mask, 2))
{
    Console.WriteLine(index); // 2, 3, 0, 1
}
```

## See also

- [`FanInSlim`](FanInSlim.md) — `Take()` returns a raw `uint` mask of fired slots.
- [`Deque<T>`](Deque.md)