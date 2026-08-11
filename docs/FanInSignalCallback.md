# FanInSignalCallback

> Namespace: `Steelax.Toolkit.HighPerformance.Concurrency.Primitives`
> Source: [`FanInSignalCallback.cs`](../src/Steelax.Toolkit.HighPerformance/Concurrency/Primitives/FanInSignalCallback.cs)

A lightweight handle that signals a specific [`FanInSlim`](FanInSlim.md) slot, **avoiding an allocation** when the callback is invoked directly.

## Usage

```csharp
var fanIn = new FanInSlim();
var callback = fanIn.GetSignalCallback(index: 3);

callback.Fire();                    // signals slot 3 immediately
Action handler = callback.Handler;  // closure-free handler (signals slot 3)
handler();
```

## API

| Member | Description |
|--------|-------------|
| `void Fire()` | Signals the bound fan-in slot immediately. |
| `Action Handler` | Gets a **closure-free** `Action` that signals the bound slot when invoked. |

## Notes

- The callback is a `readonly struct` — cheap to copy and pass around.
- Prefer `Fire()` when calling directly; use `Handler` when you need to hand an `Action` to another API.
- `Handler` throws `ArgumentNullException` if the callback is the default (`default(FanInSignalCallback)`).

## See also

- [`FanInSlim`](FanInSlim.md)
