# AsyncMarshal

> Namespace: `Steelax.Toolkit.HighPerformance`
> Source: [`AsyncMarshal.cs`](../src/Steelax.Toolkit.HighPerformance/AsyncMarshal.cs)

Provides marshaling helpers for bridging standard async enumerators into the **non-blocking primitives** used by signal-driven dataflow pipelines.

## API

| Method | Description |
|--------|-------------|
| `EventEnumerator<T> AsNonBlocking<T>(this IAsyncEnumerator<T> source)` | Wraps an `IAsyncEnumerator<T>` into a non-blocking [`EventEnumerator<T>`](EventEnumerator.md) that exposes readiness via an `OnReady` signal instead of awaiting `MoveNextAsync`. The supplied enumerator is consumed by this call. |
| `bool FireUnsafeOnCompleted<T>(scoped in ValueTask<T> task, Action? callback, OnCompletedBehavior behavior = RunCallbackInline)` | Schedules `callback` to run when `task` completes, or runs it synchronously when the task is already complete. Returns `true` when the task was already completed, `false` when a continuation was registered. With `SkipCallbackIfCompleted`, the callback is not invoked for an already-complete task. |
| `bool FireUnsafeOnCompleted(scoped in ValueTask task, Action? callback, OnCompletedBehavior behavior = RunCallbackInline)` | Non-generic `ValueTask` overload of the above. |
| `bool FireUnsafeOnCompleted(scoped in Task task, Action? callback, OnCompletedBehavior behavior = RunCallbackInline)` | `Task` overload of the above. |

`OnCompletedBehavior` (namespace `Steelax.Toolkit.HighPerformance`):
- `RunCallbackInline` (default) — invoke the callback synchronously when the operation is already complete.
- `SkipCallbackIfCompleted` — do not invoke the callback for an already-complete operation; the caller consumes the outcome itself, and the callback is registered only for in-flight operations.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var fanIn = new FanInSlim();
const int slot = 0;

// Wrap a standard async enumerator into the non-blocking adapter:
var adapter = sourceEnumerator.AsNonBlocking();

adapter.OnReady += () => fanIn.Signal(slot);
adapter.MoveNext();

await fanIn.WaitAsync();
if (adapter.GetState().IsCompletedSuccessfully)
{
    Console.WriteLine(adapter.GetResult());
}
```

## Notes

- `AsNonBlocking` is an extension method — the containing `using Steelax.Toolkit.HighPerformance;` is required to access it.
- The resulting `EventEnumerator<T>` drives the wrapped enumerator one step at a time and implements `IAsyncDisposable` (`DisposeAsync` releases the underlying enumerator).

## See also

- [`EventEnumerator<T>`](EventEnumerator.md)
- [`FanInSlim`](FanInSlim.md)
