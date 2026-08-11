# AsyncMarshal

> Namespace: `Steelax.Toolkit.HighPerformance`
> Source: [`AsyncMarshal.cs`](../src/Steelax.Toolkit.HighPerformance/AsyncMarshal.cs)

Provides marshaling helpers for bridging standard async enumerators into the **non-blocking primitives** used by signal-driven dataflow pipelines.

## API

| Method | Description |
|--------|-------------|
| `EventEnumerator<T> AsNonBlocking<T>(this IAsyncEnumerator<T> source)` | Wraps an `IAsyncEnumerator<T>` into a non-blocking [`EventEnumerator<T>`](EventEnumerator.md) that exposes readiness via an `OnReady` signal instead of awaiting `MoveNextAsync`. The supplied enumerator is consumed by this call. |

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
