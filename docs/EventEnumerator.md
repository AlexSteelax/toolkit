# EventEnumerator\<T\>

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`EventEnumerator.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/EventEnumerator.cs)

Provides **non-blocking, callback-driven** access to an `IAsyncEnumerator<T>`: each step is started via `MoveNext` without awaiting, and its outcome is later exposed through `GetState` / `GetResult`.

## Characteristics

- **Step-by-step driving** — the underlying enumerator is advanced one step at a time; each `MoveNextAsync` is started without blocking.
- **Event-driven** — `OnReady` is raised whenever an in-flight `MoveNextAsync` completes, making this type suitable for wiring an async enumerator into a signal-driven loop (e.g. a [`FanInSlim`](FanInSlim.md) slot).
- **Strict advance protocol** — `MoveNext` is only allowed when no iteration has been started yet, or after the previous value has been consumed. Calling it while the current iteration is in flight, or after an unconsumed canceled/faulted/completed iteration, throws `InvalidOperationException`.
- **Not thread-safe** — `MoveNext`, `GetState` and `GetResult` must be accessed from a single thread; `OnReady` may be raised from any thread. Subscribe to `OnReady` **before** the first `MoveNext`.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var fanIn = new FanInSlim();
const int slot = 0;

// Non-blocking adapter over IAsyncEnumerator<T> (requires System.Linq.Async for ToAsyncEnumerable):
var adapter = new[] { 1, 2 }.ToAsyncEnumerable()
    .GetAsyncEnumerator(cancellationToken)
    .AsNonBlocking();

adapter.OnReady += () => fanIn.Signal(slot);
adapter.MoveNext();

await fanIn.WaitAsync();
var slots = fanIn.Take();

if (slots.IsSet(slot) && adapter.GetState().IsCompletedSuccessfully)
{
    Console.WriteLine(adapter.GetResult()); // 1
}

await adapter.DisposeAsync();
```

## API

| Member | Description |
|--------|-------------|
| `event Action? OnReady` | Raised whenever an in-flight `MoveNextAsync` completes. Subscribe before the first `MoveNext`. |
| `void MoveNext()` | Starts the next iteration without blocking. Throws `InvalidOperationException` if the current iteration has not been consumed. |
| `EventEnumeratorState GetState()` | Gets the state of the current iteration, resolving it lazily. |
| `T GetResult()` | Returns the value produced by the current iteration; rethrows a cancellation or the captured fault. |
| `Exception? Exception` | The exception captured from a faulted operation, if any. |
| `ValueTask DisposeAsync()` | Releases the underlying async enumerator. |

`GetResult()` throws:
- `InvalidOperationException` — the current operation is still pending, or no value is available.
- `OperationCanceledException` — the current operation was canceled.
- The captured source exception — when the operation faulted.

## See also

- [`EventEnumeratorState`](ResultStates.md) — the state record returned by `GetState()`.
- [`AsyncMarshal.AsNonBlocking()`](AsyncMarshal.md) — how to obtain an `EventEnumerator<T>`.
- [`EventTask<T>`](EventTask.md) — the task counterpart.
- [`FanInSlim`](FanInSlim.md)
