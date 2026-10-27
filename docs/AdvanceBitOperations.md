# AdvanceBitOperations

> Namespace: `Steelax.Toolkit.HighPerformance`
> Source: [`AdvanceBitOperations.cs`](../src/Steelax.Toolkit.HighPerformance/AdvanceBitOperations.cs)

Functional bit-mask operations over the **32 slot indices (0..31)** packed into a `uint`.

## Characteristics

- **Plain `uint` masks** — a slot mask is a raw `uint` where each set bit is a slot index.
- **Transitions mutate in place** — `SetBit`/`UnsetBit`/`TrySetBit`/`TryUnsetBit`/`TryPopBit` take the mask by `ref` and update it; queries and factories stay pure.
- **Native composition** — combining masks (`|`, `&`, `^`, `~`, `!= 0`) is left to the built-in operators; this class only adds what they cannot express.

## Usage

The `Bit*` names are collision-safe, so the methods can be imported directly:

```csharp
using static Steelax.Toolkit.HighPerformance.AdvanceBitOperations;

var mask = 0b100101u; // slots 0, 2, 5

HasAnyBits(mask);   // true
IsBitSet(mask, 2);  // true

var removed = TryPopBit(ref mask, out var first); // first = 0, removed = true, mask now keeps slots 2, 5
```

## API

All methods are `static`. Slot indices are validated in **0..31** (ranges in **0..32**) and throw `ArgumentOutOfRangeException` otherwise.

| Method | Description |
|--------|-------------|
| `uint OfFirstBits(int count)` | The first (lowest) `count` bits set (e.g. `OfFirstBits(3) == 0b111`). |
| `uint OfLastBits(int count)` | The last (highest) `count` bits set (complement of `OfFirstBits(32 - count)`). |
| `bool HasAnyBits(uint mask)` | `true` when at least one slot is set. |
| `bool HasAllBits(uint mask)` | `true` when all 32 slots are set. |
| `bool IsBitSet(uint mask, int index)` | `true` when the slot is set. |
| `bool TryPopBit(ref uint mask, out int index)` | Removes the lowest-indexed set slot from `mask` in place (hot path, no rotation). |
| `bool TryPopBit(ref uint mask, out int index, int offset)` | Same, but scans cyclically from `offset` (queue semantics: `offset` sets where collection starts). |
| `bool TryUnsetBit(ref uint mask, int index)` | Removes a slot from `mask` when it is set; `true` when it was present. |
| `void UnsetBit(ref uint mask, int index)` | Removes a slot from `mask` idempotently. |
| `bool TrySetBit(ref uint mask, int index)` | Sets a slot in `mask` when it is not already set; `true` when it was added. |
| `void SetBit(ref uint mask, int index)` | Sets a slot in `mask` idempotently. |

## Example: consuming slots one by one

```csharp
var mask = 0b10001010u; // slots 1, 3, 7

while (TryPopBit(ref mask, out var index))
{
    Console.WriteLine($"handled slot {index}"); // 1, 3, 7
}
```

## Example: rotating through slots (queue start)

```csharp
var mask = OfFirstBits(4); // slots 0..3

while (TryPopBit(ref mask, out var index, 2))
{
    Console.WriteLine(index); // 2, 3, 0, 1
}
```

## See also

- [`FanInSlim`](FanInSlim.md) — `Take()` returns a raw `uint` mask of fired slots.
- [`FixedDeque<T>`](FixedDeque.md)