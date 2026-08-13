# EventQueue\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency`
> Sources: [`EventQueue.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/EventQueue.cs), [`EventQueue.Reader.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/EventQueue.Reader.cs), [`EventQueue.Writer.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/EventQueue.Writer.cs), [`EventQueue.ValueTaskSource.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/EventQueue.ValueTaskSource.cs)

A **bounded, event-driven SPSC buffer**: the write side offers `TryWrite` / `Complete`, the read side offers `TryRead` / `WaitToReadAsync` (a consumator-style API: non-blocking read plus async wait).

## Characteristics

- **Single-producer / single-consumer (SPSC)** — one writer drives `TryWrite` / `Complete`; one reader drives `TryRead` / `WaitToReadAsync`.
- **Bounded** — a fixed capacity; writes are rejected (return `false`) while the buffer is full.
- **Event-driven** — the reader waits on `WaitToReadAsync` and is woken when data arrives or the stream ends.
- **Counter-based readiness** — availability is modelled as a pair of monotonically increasing counters (`WriterSeq` / `ReaderSeq`, `uint`, modular 2³²). The writer publishes an item (`WriterSeq++`, release) **only after** enqueuing it; the reader consumes only while `WriterSeq - ReaderSeq > 0`. This guarantees the consumed item is fully visible and closes the enqueue→publish window. Wrap-around is natural — all checks use the delta. The single readiness core is charged (`SetResult`) at most once per signal cycle and reset by the reader before registering a new wait; a re-check closes the lost-wakeup window.
- **Write readiness** — `OnWriteReady` is raised when a previously full buffer frees capacity, so a producer can retry a rejected write.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency;

var queue = new EventQueue<int>(capacity: 4);

// Writer side:
queue.TryWrite(1);            // true
queue.TryWrite(2);            // true
queue.Complete();             // marks the stream as ended (optionally with an exception)

// Reader side:
while (true)
{
    if (queue.TryRead(out var value, out var completed))
    {
        Console.WriteLine(value);
        continue;
    }

    if (completed)
        break;                // end of stream

    await queue.WaitToReadAsync();
}
```

## API

### Constructor

`EventQueue(int capacity, bool allowSynchronousContinuations = false)`

- `capacity` — maximum number of buffered items (must be positive; throws `ArgumentOutOfRangeException`).
- `allowSynchronousContinuations` — when `true`, reader continuations may run inline on the writer's thread; when `false` (default), they are scheduled asynchronously to avoid stack growth and unexpected re-entrancy.

### Events

| Member | Description |
|--------|-------------|
| `event Action? OnWriteReady` | Raised when a previously full buffer frees capacity (the producer may retry a rejected write). |

### Writer side

| Method | Description |
|--------|-------------|
| `bool TryWrite(T item)` | Enqueues an item and signals the reader. Returns `true` on success; `false` when the buffer is full or the stream is already completed. |
| `void Complete(Exception? ex = null)` | Marks the stream as completed: no further writes are accepted; pending or subsequent reads observe the end of the stream (or rethrow `ex`). |

### Reader side

| Method | Description |
|--------|-------------|
| `bool TryRead([MaybeNullWhen(false)] out T value, out bool completed)` | Attempts to read a value without blocking. Returns `true` when a value was read (`completed == false`). When it returns `false`: the stream is over (`completed == true`) or no data is available yet (`completed == false`). If a completion exception was captured, it is **rethrown** instead of returning. |
| `ValueTask WaitToReadAsync()` | Waits asynchronously until a value is available or the stream ends, without blocking the calling thread. After the wait completes, call `TryRead` to obtain the value (or the terminal state). |

## Example: producer/consumer with retry on overflow

A rejected write means the buffer is full; the producer must observe `OnWriteReady` (or simply retry) and re-check capacity.

```csharp
var queue = new EventQueue<int>(capacity: 1);
var spin = new SpinWait();

// Producer on its own thread: retry rejected writes while the consumer frees capacity.
var producer = Task.Run(() =>
{
    for (var i = 0; i < 100; i++)
    {
        while (!queue.TryWrite(i))
            spin.SpinOnce();
    }

    queue.Complete();
});

// Consumer on another thread: drain until the stream completes.
var collected = new List<int>();
while (true)
{
    if (queue.TryRead(out var value, out var completed))
    {
        collected.Add(value);
        continue;
    }

    if (completed)
        break;

    await queue.WaitToReadAsync();
}

await producer;
```

## Notes

- A `ValueTask` returned by `WaitToReadAsync` is bound to an internal `IValueTaskSource` version token. Awaiting a **stale** token (e.g. re-awaiting an old `ValueTask` after the core was reset) throws `InvalidOperationException` — consistent with the `IValueTaskSource` contract. Await each `ValueTask` only once.
- `TryRead` rethrows the captured completion exception (from `Complete(ex)`), mirroring fault propagation to the consumer.
- The type is **not** an `IAsyncConsumator<T>` implementation — it exposes a consumator-style API with the same method shapes.

## See also

- [`EventTask<T>`](EventTask.md) / [`EventEnumerator<T>`](EventEnumerator.md) — related event-driven wrappers.
- [`Deque<T>`](Deque.md) — a simpler single-thread double-ended structure.
