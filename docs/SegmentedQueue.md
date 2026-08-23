# SegmentedQueue\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Primitives`
> Source: [`SegmentedQueue.cs`](../src/Steelax.Toolkit.HighPerformance/Primitives/SegmentedQueue.cs)

An **unbounded, non-concurrent FIFO queue** backed by a linked list of fixed-size chunks.

## Characteristics

- **Chunked storage** — elements are appended to a chain of chunks; each chunk is a buffer rented from a private, per-`T` array pool. When a chunk is fully drained it is detached and returned to the pool, so a steady-state enqueue/dequeue cycle performs **no chunk allocations**.
- **Minimum chunk size** — the constructor takes a `minimumSize`; the pool may grant a larger buffer and **its full length is used** as the chunk capacity.
- **Unbounded** — the queue never rejects a write: a full tail chunk triggers the allocation of the next one.
- **FIFO** — `Enqueue` appends, `TryDequeue` removes from the front, `TryPeek` inspects the front without consuming.
- **Single consumer thread** — the type is not thread-safe; all members must be accessed from one thread.
- **No `IDisposable`** — buffers returned to the pool are reusable; an abandoned non-empty queue lets the GC reclaim the rented arrays (pooling benefit is lost, but nothing leaks).

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Primitives;

var queue = new SegmentedQueue<int>(minimumSize: 4);

queue.Enqueue(10);
queue.Enqueue(20);
queue.Enqueue(30);

queue.Count;            // 3
queue.IsEmpty;          // false

queue.TryPeek(out var head);    // head = 10, not consumed
queue.TryDequeue(out var first); // first = 10, removed

// Draining the queue:
while (queue.TryDequeue(out var item))
{
    Console.WriteLine(item); // 20, 30
}

queue.IsEmpty;          // true
```

## API

### Properties

| Member | Type | Description |
|--------|------|-------------|
| `Count` | `int` | The number of elements currently buffered. |
| `IsEmpty` | `bool` | `true` when the queue contains no elements. |

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Enqueue(T item)` | `void` | Adds an item to the end of the queue. Always succeeds (unbounded). |
| `TryDequeue(out T item)` | `bool` | Removes and returns the front element; `false` when empty. |
| `TryPeek(out T item)` | `bool` | Returns the front element without removing it; `false` when empty. |
| `Clear()` | `void` | Removes all elements and returns every rented chunk buffer to the pool. |

## Constructor

`SegmentedQueue(int minimumSize = 64)` — throws `ArgumentOutOfRangeException` when `minimumSize` is not positive.

> **Minimum size is a lower bound:** chunks are rented from a private, per-`T` array pool, which may return an array larger than `minimumSize`. The whole rented buffer is used as the chunk capacity. Rents of the same `minimumSize` map to the same pool bucket, so chunk sizes stay consistent within a queue.

## Example: producer/consumer cycle with chunk reuse

```csharp
var queue = new SegmentedQueue<string>(minimumSize: 8);

for (var i = 0; i < 100; i++)
    queue.Enqueue($"item-{i}");

// Drain: each exhausted chunk is returned to the pool...
while (queue.TryDequeue(out var item))
{
    Console.WriteLine(item);
}

// ...and immediately reused by the next batch — no chunk allocations here.
for (var i = 0; i < 100; i++)
    queue.Enqueue($"next-{i}");
```

## See also

- [`Deque<T>`](Deque.md) — the fixed-capacity double-ended counterpart.
- [`RingCursor<T>`](RingCursor.md) — a pre-allocated, cursor-based ring.
