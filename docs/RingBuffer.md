# RingBuffer\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Primitives`
> Source: [`RingBuffer.cs`](../src/Steelax.Toolkit.HighPerformance/Primitives/RingBuffer.cs)

A fixed-capacity **FIFO** ring buffer with random access by offset from the head.

## Characteristics

- **Strict FIFO** — elements are added at the tail and removed from the head.
- **Circular storage** — slots freed by dequeue are immediately reused, so the buffer **never grows** and **does not require a power-of-two capacity**.
- **Ref access** — any element is exposed as a `ref` via the indexer, allowing struct elements to be mutated **in place** (`0` is the head, `Count - 1` is the tail).
- **Single consumer thread** — access is intended for one consumer; do not hold returned references across `TryEnqueue` / `TryDequeue`.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Primitives;

var buffer = new RingBuffer<int>(capacity: 4);

buffer.TryEnqueue(10);            // true
buffer.TryEnqueue(20);            // true
buffer.TryEnqueue(30);            // true
buffer.TryEnqueue(40);            // true

buffer.IsFull;                    // true
buffer.TryEnqueue(50);            // false — buffer is full

buffer.TryPeekHead(out var head); // head = 10 (without removal)
buffer.TryDequeue(out var first); // first = 10, removed

// Random access by offset from the head (0-based):
buffer.TryGetAt(1, out var second); // second = 30

// In-place mutation via the ref indexer:
ref var item = ref buffer[0];
item = 99;

buffer.Clear();                   // resets to the initial state
```

## API

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `Capacity` | `int` | The fixed number of slots. |
| `Count` | `int` | The number of elements currently in the buffer. |
| `IsEmpty` | `bool` | `true` when the buffer contains no elements. |
| `IsFull` | `bool` | `true` when the buffer is full. |
| `this[int offset]` | `ref T` | Reference to the element at `offset` from the head (`0`..`Count-1`). Throws `ArgumentOutOfRangeException` when out of range. |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `TryEnqueue(T item)` | `bool` | Adds an element to the tail; `false` when full. |
| `TryPeekHead(out T item)` | `bool` | Returns the head element without removing it. |
| `TryDequeue(out T item)` | `bool` | Removes and returns the head element. |
| `TryPeekTail(out T item)` | `bool` | Returns the tail element (the write target) without removing it. |
| `TryGetAt(int offset, out T item)` | `bool` | Returns the element at `offset` from the head. |
| `Clear()` | `void` | Removes all elements and resets the buffer. |

## Constructor

`RingBuffer(int capacity)` — throws `ArgumentOutOfRangeException` when `capacity` is not positive.

## Example: draining a full buffer

```csharp
var buffer = new RingBuffer<string>(capacity: 2);

buffer.TryEnqueue("a");
buffer.TryEnqueue("b");

while (buffer.TryDequeue(out var item))
{
    Console.WriteLine(item); // a, b
}

buffer.IsEmpty; // true
```

## See also

- [`SlotSet`](SlotSet.md)
- [`EventEnumerator<T>`](EventEnumerator.md)
- [`EventQueue<T>`](EventQueue.md) — the bounded SPSC queue counterpart
