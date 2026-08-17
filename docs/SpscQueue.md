# SPSC Queues and Channels

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency` / `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Sources: [`SpscQueue.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/SpscQueue.cs), [`SpscChannel.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/SpscChannel.cs), [`EventReadQueue.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/EventReadQueue.cs), [`EventWriteQueue.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/EventWriteQueue.cs), [`CompleteSignal.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)

A family of **bounded, lock-free single-producer/single-consumer FIFO buffers** built on a shared transfer core, layered with different readiness models:

| Type | Read readiness | Write readiness |
|------|----------------|-----------------|
| `SpscQueue<T>` | — (poll `TryRead`) | — (poll `TryWrite`) |
| `SpscChannel<T>` | `WaitToReadAsync()` | `WaitToWriteAsync()` |
| `EventReadQueue<T>` | `OnReadReady` event | `WaitToWriteAsync()` |
| `EventWriteQueue<T>` | `WaitToReadAsync()` | `OnWriteReady` event |

## Characteristics

- **Single-producer / single-consumer (SPSC)** — one writer drives the write side, one reader drives the read side. Access from other threads is not supported.
- **Bounded** — a fixed capacity; writes are rejected (return `false`) while the buffer is full.
- **Counter-based transfer** — the core is a fixed-size circular buffer of power-of-two length. Availability is modelled as a pair of monotonically increasing counters (`WriterSeq` / `ReaderSeq`, `uint`, modular 2³²); the writer publishes an item (`WriterSeq++`, release) only after writing it, and the reader consumes only while `WriterSeq - ReaderSeq > 0`. Wrap-around is natural — all checks use the delta.
- **Readiness hooks** — the core exposes `OnFirstInsertOrComplete` (first item written or stream completed) and `OnFreeSpace` (a slot of a full buffer freed); derived types layer a signal or an event on top.
- **Edge-triggered** — readiness is raised only on transitions, not on every operation.

## Variants

### `SpscQueue<T>` — the core

The transfer core alone: `TryWrite` / `TryRead` / `TryComplete` / `IsCompleted` / `Count`. No waiting — a poller retries rejected writes/reads.

```csharp
var q = new SpscQueue<int>(4);
q.TryWrite(1);
while (q.TryRead(out var v)) { }
```

### `SpscChannel<T>` — full await (channel-like)

Adds `WaitToReadAsync()` / `WaitToWriteAsync()` for asynchronous readiness on both sides.

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency;

var channel = new SpscChannel<int>(capacity: 4);

// Consumer: wait for data, then read.
while (true)
{
    if (channel.TryRead(out var value))
    {
        Console.WriteLine(value);
        continue;
    }
    if (channel.IsCompleted)
        break;
    await channel.WaitToReadAsync();
}

// Producer: wait for free capacity, then write.
if (!channel.TryWrite(1))
    await channel.WaitToWriteAsync();
```

### `EventReadQueue<T>` — reader on subscription

The reader observes readiness via the `OnReadReady` event (raised when data or the end of the stream becomes available); the writer waits via `WaitToWriteAsync()`.

### `EventWriteQueue<T>` — writer on subscription

The writer observes readiness via the `OnWriteReady` event (raised when a slot of a full buffer is freed); the reader waits via `WaitToReadAsync()`.

## API

All types inherit the core from `SpscQueue<T>`. Constructor: `(int capacity, bool allowSynchronousContinuations = true)` — the latter inlines continuations on the signalling thread when `true` (default), or schedules them asynchronously when `false`.

### Core members

| Member | Description |
|--------|-------------|
| `bool TryWrite(T item)` | Enqueues an item. Returns `true` on success; `false` when the buffer is full. A write to a **closed** stream throws `InvalidOperationException`. |
| `bool TryRead([MaybeNullWhen(false)] out T value)` | Attempts to read a value without blocking. Returns `false` when the buffer is empty (check `IsCompleted` to distinguish an ended stream); rethrows the completion exception if the stream was faulted. |
| `bool TryComplete(Exception? ex = null)` | Closes the stream; further writes throw and subsequent reads observe the end of the stream (or rethrow `ex`). |
| `bool IsCompleted` | `true` when the stream ended and the buffer is empty. |
| `int Count` | Best-effort number of buffered items (delta of the counters). |

### Readiness (per variant)

| Variant | Members |
|---------|---------|
| `SpscChannel<T>` | `ValueTask WaitToReadAsync()`, `ValueTask WaitToWriteAsync()` |
| `EventReadQueue<T>` | `event Action? OnReadReady`, `ValueTask WaitToWriteAsync()` |
| `EventWriteQueue<T>` | `event Action? OnWriteReady`, `ValueTask WaitToReadAsync()` |

## Example: producer/consumer with wait on overflow

`SpscChannel` lets both sides block instead of spinning:

```csharp
var channel = new SpscChannel<int>(capacity: 1);

var producer = Task.Run(async () =>
{
    for (var i = 0; i < 100; i++)
    {
        while (!channel.TryWrite(i))
            await channel.WaitToWriteAsync();
    }
    channel.TryComplete();
});

var collected = new List<int>();
while (true)
{
    if (channel.TryRead(out var value))
    {
        collected.Add(value);
        continue;
    }
    if (channel.IsCompleted)
        break;
    await channel.WaitToReadAsync();
}

await producer;
```

## Notes

- A `ValueTask` returned by `WaitToReadAsync`/`WaitToWriteAsync` is bound to an internal `IValueTaskSource` version token (via [`CompleteSignal`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)). Await each `ValueTask` only once.
- `TryRead` rethrows the captured completion exception (from `TryComplete(ex)`), mirroring fault propagation to the consumer.
- The family is **not** an `IAsyncConsumator<T>` implementation — it exposes consumator-style APIs with the same method shapes.

## See also

- [`EventTask<T>`](EventTask.md) / [`EventEnumerator<T>`](EventEnumerator.md) — related event-driven wrappers.
- [`Deque<T>`](Deque.md) — a simpler single-thread double-ended structure.
