# Result States: EventEnumeratorState / EventTaskState

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Sources: [`EventEnumeratorState.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/EventEnumeratorState.cs), [`EventTaskState.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/EventTaskState.cs)

Both event types expose a compact `readonly record struct` state describing the outcome of an operation. **Exactly one flag is set** (except the default value, for which none of the flags is set).

## EventEnumeratorState

Used by [`EventEnumerator<T>`](EventEnumerator.md).

| Factory | Flag | Meaning |
|---------|------|---------|
| `Pending()` | `IsPending` | The operation has not completed yet. |
| `CompletedSuccessfully()` | `IsCompletedSuccessfully` | A value is available for the current step. |
| `Canceled()` | `IsCanceled` | The operation was canceled. |
| `Faulted()` | `IsFaulted` | The operation faulted. |
| `EndOfStream()` | `IsEndOfStream` | The enumerator reached the end of the sequence. |

## EventTaskState

Used by [`EventTask<T>`](EventTask.md) / `EventTask` — same shape without `EndOfStream`.

| Factory | Flag | Meaning |
|---------|------|---------|
| `Pending()` | `IsPending` | The task has not completed yet. |
| `CompletedSuccessfully()` | `IsCompletedSuccessfully` | The task completed with a value. |
| `Canceled()` | `IsCanceled` | The task was canceled. |
| `Faulted()` | `IsFaulted` | The task faulted. |

## Shared members

| Member | Description |
|--------|-------------|
| `bool IsCompleted` | Derived convenience covering any completed state (success, cancellation, fault and — for enumerator — end-of-stream). |
| `static implicit operator bool` | Converts the state to `bool`; `true` only when `IsCompletedSuccessfully`. |

## Example

```csharp
var state = adapter.GetState();

if (state.IsCompletedSuccessfully)
{
    var value = adapter.GetResult();
}
else if (state.IsPending)
{
    // still in flight — await OnReady / FanInSlim signal
}
else if (state.IsCanceled)
{
    // handle cancellation
}
else if (state.IsFaulted)
{
    var ex = adapter.Exception;
}
else if (state.IsEndOfStream)
{
    // sequence finished
}
```

## See also

- [`EventEnumerator<T>`](EventEnumerator.md)
- [`EventTask<T>`](EventTask.md)
