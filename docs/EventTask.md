# EventTask\<T\> / EventTask

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`EventTask.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/EventTask.cs)

A **non-blocking, event-driven** wrapper over a `ValueTask<T>` (or `ValueTask`): observes the task **without awaiting it** and raises `OnReady` when it completes, integrating with signal-driven loops (for example, a [`FanInSlim`](FanInSlim.md) slot).

## Characteristics

- **Observe without await** — `Observe` consumes the supplied task; do not await it elsewhere.
- **Single in-flight operation** — observing while the previous task is still in flight throws `InvalidOperationException`.
- **Subscribe before observing** — subscribe to `OnReady` **before** the first `Observe`; the continuation is captured at observe time (a subscription added later skips a task already in flight).
- **Lazy resolution** — the outcome is resolved at most once via `GetState` / `GetResult` / `Exception`.
- **Not thread-safe** — `Observe`, `GetState`, `GetResult` and `Exception` must be accessed from a single thread; `OnReady` may be raised from any thread.

## Usage

```csharp
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var task = new EventTask<int>();
task.OnReady += () => Console.WriteLine("task ready");

task.Observe(someValueTask);          // non-blocking

var state = task.GetState();          // EventTaskState
if (state.IsCompletedSuccessfully)
{
    var value = task.GetResult();     // the value (rethrows on fault/cancel)
}
```

## API

### `EventTask<T>` / `EventTask` (non-generic)

| Member | Description |
|--------|-------------|
| `event Action? OnReady` | Raised when the observed task completes. Subscribe before the first `Observe`. |
| `void Observe(ValueTask<T> task)` / `void Observe(ValueTask task)` | Starts observing a task without awaiting it. Throws `InvalidOperationException` when the previous task is still in flight. |
| `EventTaskState GetState()` | Gets the state of the observed task, resolving it lazily. |
| `T GetResult()` | Returns the resolved result; rethrows a cancellation or the captured fault. |
| `Exception? Exception` | The exception captured from a faulted task, if any. |

`GetResult()` throws:
- `InvalidOperationException` — the observed task has not completed yet.
- `OperationCanceledException` — the observed task was canceled.
- The captured source exception — when the task faulted.

## Example: integration with FanInSlim

```csharp
var fanIn = new FanInSlim();
const int slot = 0;

var task = new EventTask<int>();
task.OnReady += () => fanIn.Signal(slot);

task.Observe(someValueTask);

await fanIn.WaitAsync();
if (fanIn.Take().IsSet(slot))
{
    Console.WriteLine(task.GetResult());
}
```

## See also

- [`EventTaskState`](ResultStates.md) — the state record returned by `GetState()`.
- [`EventEnumerator<T>`](EventEnumerator.md) — the enumerator counterpart.
- [`FanInSlim`](FanInSlim.md) / [`BitTaskAny`](BitTaskAny.md) / [`EventQueue<T>`](EventQueue.md)
