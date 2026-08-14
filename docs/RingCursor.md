# RingCursor\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Primitives`
> Source: [`RingCursor.cs`](../src/Steelax.Toolkit.HighPerformance/Primitives/RingCursor.cs)

A fixed-capacity, **pre-allocated SPSC ring** with cursor-based access: elements are created once by a factory, then a window of occupied slots is grown via `Advance` and observed via `Peek` — with **no allocations on the hot path**.

## Characteristics

- **Pre-allocation** — all `Capacity` slots are created up front by the supplied factory; the ring starts empty and nothing is ever allocated or physically removed afterwards.
- **Cursor-based window** — the consumer grows a window of occupied slots (`AdvanceFirst` / `AdvanceLast`), reads positions (`PeekFirst` / `PeekLast`) and shrinks it (`ShrinkFirst` / `ShrinkLast`).
- **Position-based access** — methods return an `Index` offset, and data access happens through `this[Index]`, returning a `ref` for in-place mutation.
- **Zero-allocation reuse** — consumed slots are logically released but their elements remain in the buffer, so elements can be reset and reused for the next cycle.
- **Power-of-two masking** — the internal buffer is rounded up to the nearest power of two; ring indices use a bitwise mask (`& mask`).
- **Single consumer thread** — do not hold returned references across a mutating operation (`Advance*` / `Shrink*`).

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Primitives;

var ring = new RingCursor<Message>(capacity: 64, static () => new Message());

// Reserve a slot from the back and mutate it in place.
if (ring.AdvanceLast(out var offset))
{
    ref var msg = ref ring[offset];
    msg.Fill(...); // no allocation, reuses the pre-allocated instance
}

// Read the first occupied slot without changing the window.
if (ring.PeekFirst(out var first))
{
    var head = ring[first];
}

// Cyclic access: offset >= capacity (or negative) wraps by Capacity.
ref var wrapped = ref ring.GetAt(64); // == ring.GetAt(0)

// Release the front slot; the element stays in the buffer for reuse.
ring.ShrinkFirst();
```

## API

### Constructor

`RingCursor(int capacity, Func<T> factory)` — throws `ArgumentOutOfRangeException` when `capacity` is not positive, and `ArgumentNullException` when `factory` is `null`. Pre-allocates every slot (the internal buffer rounds up to a power of two).

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `Capacity` | `int` | The strict upper bound of occupied slots (the internal buffer rounds up to a power of two). |
| `Count` | `int` | The number of elements currently occupied in the window. |
| `IsEmpty` | `bool` | `true` when the window contains no occupied elements. |
| `this[Index offset]` | `ref T` | Reference to the element at `offset` from the front of the window (`0`..`Count-1`, or `^1`..`^Count`). Throws `ArgumentOutOfRangeException` when out of range. |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `PeekFirst(out Index offset)` | `bool` | Returns the position of the first element without changing the window. |
| `PeekLast(out Index offset)` | `bool` | Returns the position of the last element without changing the window. |
| `AdvanceFirst(out Index offset)` | `bool` | Reserves the next slot at the front (window grows by one) and returns its position; identical to `AdvanceLast` when empty. |
| `AdvanceLast(out Index offset)` | `bool` | Reserves the next slot at the back (window grows by one) and returns its position. |
| `ShrinkFirst()` | `void` | Shrinks the window from the front without physically removing the element. |
| `ShrinkLast()` | `void` | Shrinks the window from the back without physically removing the element. |
| `GetAt(Index offset)` | `ref T` | Returns a reference to the element, normalizing `offset` cyclically by `Capacity` (negative and `>= Capacity` wrap around), then resolving through the indexer. |

## Notes

- When the window is **empty**, `AdvanceFirst` and `AdvanceLast` reserve the same first slot.
- When the window is **full**, `Advance*` returns `false`.
- `GetAt` normalizes by `Capacity` (not by the physical buffer size); `^N` is treated as `[Capacity - N]`. After normalization it delegates to the strict window-checking indexer.
- There is no `Clear` — reset the ring by shrinking the window to zero (`while (ring.Count > 0) ring.ShrinkLast();`).

## See also

- [`Deque<T>`](Deque.md) — a fixed-capacity double-ended queue with symmetric end access.
- [`EventQueue<T>`](EventQueue.md) — the bounded SPSC event-driven queue, also based on power-of-two masking.
- [`SlotSet`](SlotSet.md)
