# Conduit\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Collections`
> Source: [`Conduit.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Collections/Conduit.cs), [`Conduit.Reader.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Collections/Conduit.Reader.cs), [`Conduit.Writer.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Collections/Conduit.Writer.cs), [`Conduit.Duplex.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Collections/Conduit.Duplex.cs), [`CompleteSignal.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/CompleteSignal.cs)

A bounded, **lock-free single-producer / single-consumer** FIFO over a power-of-two circular buffer, with optional asynchronous readiness on either side.

`Conduit<T>` is the single transfer primitive of the SPSC family: it replaces the former `SpscCoreQueue<T>` / `SpscQueue<T>` / `SpscChannel<T>` / `SpscChannelReader<T>` / `SpscChannelWriter<T>` split. There is no role hierarchy — one type exposes the full read/write surface. To expose a narrower role (e.g. a read-only channel to a consumer), wrap `Conduit<T>` in your own class or expose it through your own interface; nothing prevents that.

## Characteristics

- **Lock-free SPSC, single ring**. Exactly two parties share an instance: one *writer* thread calls `TryWrite`/`TryComplete`/`WaitToWriteAsync`; one *reader* thread calls `TryRead`/`TryPeek`/`WaitToReadAsync`. Driving a side from another thread, or both sides from one thread, is not supported.
- **Power-of-two storage**. Capacity is rounded up to a power of two; the ring holds `capacity + 1` slots so the empty and full states stay distinguishable. `Capacity` remains the strict upper bound of buffered items.
- **Readiness signals (optional)**. A `ConduitBehavior` flag per side (`AwaitableReader`, `AwaitableWriter`) allocates a `CompleteSignal` for that side, enabling `WaitToReadAsync`/`WaitToWriteAsync` on it. Either side may instead be driven by an event (`OnReadReady`/`OnWriteReady`) and/or plain polling.
- **Cache-line separated**. The writer and reader sequence counters (and their per-side companions) sit in distinct 64-byte cache lines, so neither side invalidates a line the other writes on the hot path — the only cross-core traffic is the inherent publish/consume of the counters.
- **Liveness & abort**. `Version` is a monotonic activity counter for watchdog checks; `TryTerminate` hard-aborts the stream (unlike `TryComplete`, buffered data is not drained and both sides observe the exception immediately).

## Behavior (constructor)

```csharp
new Conduit<int>(capacity, ConduitBehavior behavior = ConduitBehavior.Default)
```

`ConduitBehavior` is a `[Flags]` enum:

| Flag | Effect |
|------|--------|
| `Default` (0) | No awaitable signal: both sides drive readiness via events or polling. `WaitToReadAsync`/`WaitToWriteAsync` return a liveness snapshot (see below). |
| `AwaitableReader` | Allocate the reader's `CompleteSignal`; `WaitToReadAsync` awaits real read-readiness. |
| `AwaitableWriter` | Allocate the writer's `CompleteSignal`; `WaitToWriteAsync` awaits freed-capacity. |

Combine flags with `|` for full-duplex waiting: `ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter`.

## Usage

### Bare conduit (polling / events)

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

var queue = new Conduit<int>(4);

queue.TryWrite(10);          // true
queue.TryWrite(20);          // true

queue.Count;                 // 2
queue.IsFull;                // false

queue.TryPeek(out var head); // head = 10, not consumed
queue.TryRead(out var v);    // v = 10

while (queue.TryRead(out var item))   // drain
{
    Console.WriteLine(item);          // 20
}
```

### Awaitable full-duplex channel

```csharp
var queue = new Conduit<int>(
    capacity: 4,
    ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter);

// Writer
await queue.WaitToWriteAsync();          // true when a slot is free
queue.TryWrite(42);                      // true

// Reader
var r = Task.Run(async () =>
{
    await queue.WaitToReadAsync();
    return queue.TryRead(out var item) ? item : 0;
});

queue.TryComplete();
await r;                                 // completes after drain / end-of-stream
```

## API

### Core state

| Member | Type | Description |
|--------|------|-------------|
| `Capacity` | `uint` | The maximum number of buffered items (strict bound). Read-only. |
| `Count` | `int` | Current number of buffered items (best-effort volatile delta, safe from either side). |
| `IsCompleted` | `bool` | `true` when the stream has ended and the buffer is empty. |
| `IsFull` | `bool` | `true` when all `Capacity` slots are occupied (does not consider completion). Safe from either side. |
| `Version` | `int` | Monotonic activity counter for liveness/watchdog checks. |

### Reader side

| Member | Returns | Description |
|--------|---------|-------------|
| `TryRead(out T value)` | `bool` | Non-blocking read; `true` when a value was consumed. `false` on an empty buffer — check `IsCompleted` to distinguish an ended stream. On a faulted empty stream the exception is rethrown. |
| `TryPeek(out T value)` | `bool` | Like `TryRead` but does not consume / advance the reader sequence. |
| `WaitToReadAsync()` | `ValueTask<bool>` | Awaits data availability; resolves `false` at end-of-stream. **Bare conduit** (no `AwaitableReader`): returns `!IsCompleted` immediately (a liveness probe, not a real wait). |

`OnReadReady` (`event Action?`) fires on the writer side when the queue becomes readable (first item written, or the stream completed) so the reader can re-check. Includes the terminal `Readable`, so a reader that also needs to observe end-of-stream subscribes here and reads until `TryRead` returns `false`.

### Writer side

| Member | Returns | Description |
|--------|---------|-------------|
| `TryWrite(T item)` | `bool` | Non-blocking write; `false` when the buffer is full. Writing to a closed stream throws. |
| `WaitToWriteAsync()` | `ValueTask<bool>` | Awaits freed capacity; resolves `false` at end-of-stream. **Bare conduit** (no `AwaitableWriter`): returns `!IsCompleted` immediately (liveness probe). |
| `IsFull` | — | see Core state. |

`OnWriteReady` (`event Action?`) fires on the reader side when a slot of a full buffer is freed, so a writer can retry a rejected write.

### Terminal

| Member | Returns | Description |
|--------|---------|-------------|
| `TryComplete(Exception? ex = null)` | `bool` | Closes the stream (writer). If the buffer is empty completion latches at once; otherwise the reader drains first. Raises `OnReadReady` so the reader re-checks. `false` if already closed. |
| `TryTerminate(Exception ex)` | `bool` | Hard-aborts: latches completion immediately and surfaces `ex` on both sides, discarding buffered data. For watchdog stall recovery. |

## Events vs signals

The **synchronous** readiness surface is the two events (`OnReadReady` reader, `OnWriteReady` writer); the **asynchronous** readiness surface is `WaitToReadAsync`/`WaitToWriteAsync`, active only when the matching `ConduitBehavior.*` flag created a signal. Because events and signals both come from the same transitions, a side can be consumed via an event alone, a signal alone, or both. A bare conduit (behavior `Default`) is exercised entirely through events and/or polling — its wait methods are liveness probes, still consistent with completion.

## Example: watchdog liveness and abort

```csharp
var queue = new Conduit<int>(4);

// ... producer/consumer running ...
var version = queue.Version;
if (/* version has not advanced for too long */)
{
    queue.TryTerminate(new TimeoutException("producer stalled"));
    // reader observes the exception on its next TryRead
}
```

## See also

- [`CompleteSignal`](CompleteSignal.md) — the edge-triggered readiness signal backing `WaitToReadAsync`/`WaitToWriteAsync`.
- [`SegmentedQueue<T>`](SegmentedQueue.md) — the unbounded, non-concurrent chained counterpart.
- [`RingCursor<T>`](RingCursor.md), [`Deque<T>`](Deque.md) — fixed-capacity non-concurrent structures.
