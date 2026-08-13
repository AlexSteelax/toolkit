# Deque\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Primitives`
> Source: [`Deque.cs`](../src/Steelax.Toolkit.HighPerformance/Primitives/Deque.cs)

A fixed-capacity **double-ended queue** with symmetric access to both ends and random access by offset from the first element.

## Characteristics

- **Double-ended** — elements are added to and removed from either end: `TryAddFirst` / `TryAddLast`, `TryPopFirst` / `TryPopLast`, `TryPeekFirst` / `TryPeekLast`.
- **Circular storage** — slots freed by a pop are immediately reused, so the deque **never grows**.
- **Power-of-two masking** — the internal buffer is rounded up to the nearest power of two, so ring indices are computed with a bitwise mask (`index & mask`) instead of a modulo. The extra slots are **reserved and never exposed**; `Capacity` remains the strict upper bound of buffered elements.
- **Ref access** — any element is exposed as a `ref` via the indexers `this[int]` and `this[Index]`, allowing struct elements to be mutated **in place** (`0` / `^Count` is the first element, `Count - 1` / `^1` is the last).
- **Single consumer thread** — access is intended for one consumer; do not hold returned references across a mutating operation (`TryAdd*` / `TryPop*`).

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Primitives;

var deque = new Deque<int>(capacity: 4);

deque.TryAddLast(10);             // true
deque.TryAddLast(20);             // true
deque.TryAddLast(30);             // true
deque.TryAddLast(40);             // true

deque.IsFull;                     // true
deque.TryAddLast(50);             // false — deque is full

deque.TryPeekFirst(out var first); // first = 10 (without removal)
deque.TryPeekLast(out var last);   // last = 40 (without removal)
deque.TryPopFirst(out var popped); // popped = 10, removed

// Add to the other end:
deque.TryAddFirst(0);             // true

// Random access by offset from the first element (0-based):
deque.TryGetAt(1, out var second); // second = 20

// In-place mutation via the ref indexers:
ref var item = ref deque[0];       // ^Count also works
item = 99;

// Index from the end:
ref var tail = ref deque[^1];

deque.Clear();                    // resets to the initial state
```

## API

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `Capacity` | `int` | The strict upper bound of buffered elements (the internal buffer rounds up to a power of two; extra slots are reserved). |
| `Count` | `int` | The number of elements currently in the deque. |
| `IsEmpty` | `bool` | `true` when the deque contains no elements. |
| `IsFull` | `bool` | `true` when the deque is full. |
| `this[int offset]` | `ref T` | Reference to the element at `offset` from the first (`0`..`Count-1`). Throws `ArgumentOutOfRangeException` when out of range. |
| `this[Index index]` | `ref T` | Reference to the element at `index` from either end (`0`..`Count-1`, or `^1`..`^Count`). Throws `ArgumentOutOfRangeException` when out of range. |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `TryAddFirst(T item)` | `bool` | Adds an element to the front; `false` when full. |
| `TryAddLast(T item)` | `bool` | Adds an element to the back; `false` when full. |
| `TryPeekFirst(out T item)` | `bool` | Returns the first element without removing it. |
| `TryPeekLast(out T item)` | `bool` | Returns the last element without removing it. |
| `TryPopFirst(out T item)` | `bool` | Removes and returns the first element. |
| `TryPopLast(out T item)` | `bool` | Removes and returns the last element. |
| `TryGetAt(int offset, out T item)` | `bool` | Returns the element at `offset` from the first. |
| `Clear()` | `void` | Removes all elements and resets the deque. |

## Constructor

`Deque(int capacity)` — throws `ArgumentOutOfRangeException` when `capacity` is not positive.

> **Capacity and reserved slots:** the internal buffer is allocated as the nearest power of two ≥ `capacity` (for example `capacity: 5` allocates 8 slots). The extra slots are reserved for masking and are never exposed: writes are rejected once `Count` reaches `Capacity`.

## Example: draining a full deque from the front

```csharp
var deque = new Deque<string>(capacity: 2);

deque.TryAddLast("a");
deque.TryAddLast("b");

while (deque.TryPopFirst(out var item))
{
    Console.WriteLine(item); // a, b
}

deque.IsEmpty; // true
```

## Example: LIFO (stack) via the back end

```csharp
var stack = new Deque<int>(capacity: 8);

stack.TryAddLast(1);
stack.TryAddLast(2);
stack.TryAddLast(3);

stack.TryPopLast(out var top); // top = 3
```

## See also

- [`SlotSet`](SlotSet.md)
- [`EventEnumerator<T>`](EventEnumerator.md)
- [`EventQueue<T>`](EventQueue.md) — the bounded SPSC queue counterpart, also based on power-of-two masking
