# SPSC Queues and Channels

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Channels` / `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Sources: [`SpscQueue.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Channels/SpscQueue.cs), [`SpscChannel.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Channels/SpscChannel.cs), [`SpscChannelReader.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Channels/SpscChannelReader.cs), [`SpscChannelWriter.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Channels/SpscChannelWriter.cs), [`CompleteSignal.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)

A family of **bounded, lock-free single-producer/single-consumer FIFO buffers** built on a shared transfer core, layered with different readiness models. All types are reached through **role views** — `Reader` / `Writer` — so the single-producer/single-consumer contract is enforced at the type level, not just by documentation.

| Type | Read readiness | Write readiness |
|------|----------------|-----------------|
| `SpscQueue<T>` | — (poll `TryRead` via `Reader`) | — (poll `TryWrite` via `Writer`) |
| `SpscChannel<T>` | `WaitToReadAsync()` | `WaitToWriteAsync()` |
| `SpscChannelReader<T>` | `WaitToReadAsync()` | `OnWriteReady` event |
| `SpscChannelWriter<T>` | `OnReadReady` event | `WaitToWriteAsync()` |

## Characteristics

- **Single-producer / single-consumer (SPSC)** — one writer drives the write side, one reader drives the read side. The roles are separated into `Writer` and `Reader` views: a writer cannot call read operations and vice versa. Access from other threads is not supported.
- **Bounded** — a fixed capacity; writes are rejected (return `false`) while the buffer is full.
- **Counter-based transfer** — the core is a fixed-size circular buffer of power-of-two length. Availability is modelled as a pair of monotonically increasing counters (`WriterSeq` / `ReaderSeq`, `uint`, modular 2³²); the writer publishes an item (`WriterSeq++`, release) only after writing it, and the reader consumes only while `WriterSeq - ReaderSeq > 0`. Wrap-around is natural — all checks use the delta.
- **Readiness hooks** — the core exposes `OnFirstInsertOrComplete` (first item written or stream completed), `OnFreeSpace` (a slot of a full buffer freed), `OnDrained` and `OnFilled`; derived channel types layer a signal or an event on top.
- **Edge-triggered** — readiness is raised only on transitions, not on every operation.

## Variants

### `SpscQueue<T>` — the core

The transfer core alone: `TryWrite` / `TryRead` / `TryComplete` / `IsCompleted` / `Count`, exposed through the `QueueWriter<T>` / `QueueReader<T>` role views. No waiting — a poller retries rejected writes/reads.

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

var queue = new SpscQueue<int>(4);
var writer = queue.Writer;
var reader = queue.Reader;

writer.TryWrite(1);
while (reader.TryRead(out var value)) { }
```

> The bare queue does not raise readiness signals: `WaitToReadAsync` / `WaitToWriteAsync` are supported only by derived channel types.

### `SpscChannel<T>` — full await (channel-like)

Adds `WaitToReadAsync()` / `WaitToWriteAsync()` for asynchronous readiness on both sides. The role views are `ChannelReader<T>` / `ChannelWriter<T>`.

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

var channel = new SpscChannel<int>(capacity: 4);
var writer = channel.Writer;
var reader = channel.Reader;

// Consumer: wait for data, then read.
while (true)
{
    if (reader.TryRead(out var value))
    {
        Console.WriteLine(value);
        continue;
    }
    if (reader.IsCompleted)
        break;
    await reader.WaitToReadAsync();
}

// Producer: wait for free capacity, then write.
if (!writer.TryWrite(1))
    await writer.WaitToWriteAsync();
```

### `SpscChannelReader<T>` — writer on subscription

The **reader** waits via `WaitToReadAsync()`; the **writer** observes readiness via the `OnWriteReady` event (raised when a slot of a full buffer is freed).

```csharp
var channel = new SpscChannelReader<int>(capacity: 4);
var reader = channel.Reader;

channel.OnWriteReady += () => { /* a slot was freed — retry the write */ };
await reader.WaitToReadAsync();
```

### `SpscChannelWriter<T>` — reader on subscription

The **writer** waits via `WaitToWriteAsync()`; the **reader** observes readiness via the `OnReadReady` event (raised when data or the end of the stream becomes available).

```csharp
var channel = new SpscChannelWriter<int>(capacity: 4);
var writer = channel.Writer;

channel.OnReadReady += () => { /* data arrived — try to read */ };
await writer.WaitToWriteAsync();
```

## API

All types inherit the core from `SpscQueue<T>`. Constructor: `(int capacity, bool allowSynchronousContinuations = true)` — the latter inlines continuations on the signalling thread when `true` (default), or schedules them asynchronously when `false`.

The underlying core members are `protected internal` and are reached through the role views:

| Role view | Members |
|-----------|---------|
| `QueueReader<T>` / `ChannelReader<T>` | `bool TryRead(out T value)`, `bool IsReadable`, `bool IsCompleted`, `int Count` (+ `ValueTask WaitToReadAsync()` for channel readers) |
| `QueueWriter<T>` / `ChannelWriter<T>` | `bool TryWrite(T item)`, `bool TryComplete(Exception? ex = null)`, `bool IsWritable`, `int Count` (+ `ValueTask WaitToWriteAsync()` for channel writers) |

### Core members

| Member | Description |
|--------|-------------|
| `bool TryWrite(T item)` | Enqueues an item. Returns `true` on success; `false` when the buffer is full. A write to a **closed** stream throws `InvalidOperationException`. |
| `bool TryRead([MaybeNullWhen(false)] out T value)` | Attempts to read a value without blocking. Returns `false` when the buffer is empty (check `IsCompleted` to distinguish an ended stream); rethrows the completion exception if the stream was faulted. |
| `bool TryComplete(Exception? ex = null)` | Closes the stream; further writes throw and subsequent reads observe the end of the stream (or rethrow `ex`). |
| `bool IsReadable` | Best-effort check that the buffer holds at least one item — a `TryRead` would succeed. |
| `bool IsWritable` | Best-effort check that the buffer has at least one free slot — a `TryWrite` would not be rejected. |
| `bool IsCompleted` | `true` when the stream ended and the buffer is empty. |
| `int Count` | Best-effort number of buffered items (delta of the counters). |

### Readiness (per variant)

| Variant | Members |
|---------|---------|
| `SpscChannel<T>` | `WaitToReadAsync()`, `WaitToWriteAsync()` |
| `SpscChannelReader<T>` | `OnWriteReady` event, `WaitToReadAsync()` |
| `SpscChannelWriter<T>` | `OnReadReady` event, `WaitToWriteAsync()` |

## Example: producer/consumer with wait on overflow

`SpscChannel` lets both sides block instead of spinning:

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

var channel = new SpscChannel<int>(capacity: 1);
var writer = channel.Writer;
var reader = channel.Reader;

var producer = Task.Run(async () =>
{
    for (var i = 0; i < 100; i++)
    {
        while (!writer.TryWrite(i))
            await writer.WaitToWriteAsync();
    }
    writer.TryComplete();
});

var collected = new List<int>();
while (true)
{
    if (reader.TryRead(out var value))
    {
        collected.Add(value);
        continue;
    }
    if (reader.IsCompleted)
        break;
    await reader.WaitToReadAsync();
}

await producer;
```

## Notes

- Role views are `readonly struct`s holding a single reference: they add **zero allocation and zero boxing**, and the thin forwarders inline away on the hot path. The same structure allows adapting them to interfaces via generic wrappers (`where T : struct, I{...}`).
- A `ValueTask` returned by `WaitToReadAsync`/`WaitToWriteAsync` is bound to an internal `IValueTaskSource` version token (via [`CompleteSignal`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)). Await each `ValueTask` only once.
- `TryRead` rethrows the captured completion exception (from `TryComplete(ex)`), mirroring fault propagation to the consumer.
- The family is **not** an `IAsyncConsumator<T>` implementation — it exposes consumator-style APIs with the same method shapes.

## See also

- [`EventTask<T>`](EventTask.md) / [`EventEnumerator<T>`](EventEnumerator.md) — related event-driven wrappers.
- [`Deque<T>`](Deque.md) — a simpler single-thread double-ended structure.
