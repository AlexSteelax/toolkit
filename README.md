# Steelax.Toolkit.HighPerformance

[![NuGet](https://img.shields.io/nuget/v/Steelax.Toolkit.HighPerformance.svg)](https://www.nuget.org/packages/Steelax.Toolkit.HighPerformance)
[![Downloads](https://img.shields.io/nuget/dt/Steelax.Toolkit.HighPerformance.svg)](https://www.nuget.org/packages/Steelax.Toolkit.HighPerformance/)

**Steelax.Toolkit.HighPerformance** — a collection of high-performance primitives for building non-blocking, signal-driven async pipelines:

- **lock-free / lightweight concurrency** — fan-in signaling, event-driven task/enumerator wrappers, task-slot sets;
- **low-allocation data structures** — fixed-capacity deque and bitmask-based slot sets.

The library is the foundation for the [Steelax.Pufflow](https://www.nuget.org/packages/Steelax.Pufflow) dataflow pipelines, but every primitive is a standalone public API.

---

## 📦 Installation

```
dotnet add package Steelax.Toolkit.HighPerformance
```

---

## 📚 Documentation

### Data Structures

| Type | Namespace | Docs |
|------|-----------|------|
| `Deque<T>` | `Steelax.Toolkit.HighPerformance.Primitives` | [📄 Deque](docs/Deque.md) |
| `RingCursor<T>` | `Steelax.Toolkit.HighPerformance.Primitives` | [📄 RingCursor](docs/RingCursor.md) |
| `SlotSet` | `Steelax.Toolkit.HighPerformance.Primitives` | [📄 SlotSet](docs/SlotSet.md) |

### Concurrency Primitives

| Type | Namespace | Docs |
|------|-----------|------|
| `SpscQueue<T>` / `SpscChannel<T>` / `SpscChannelReader<T>` / `SpscChannelWriter<T>` (+ `Queue*`/`Channel*` role views) | `...Concurrency.Channels` | [📄 SPSC Queues](docs/SpscQueue.md) |
| `FanInSlim` | `...Concurrency.Primitives` | [📄 FanInSlim](docs/FanInSlim.md) |
| `FanInSignalCallback` | `...Concurrency.Primitives` | [📄 FanInSignalCallback](docs/FanInSignalCallback.md) |
| `BitTaskAny` | `...Concurrency.Primitives` | [📄 BitTaskAny](docs/BitTaskAny.md) |
| `EventTask<T>` / `EventTask` | `...Concurrency.Primitives` | [📄 EventTask](docs/EventTask.md) |
| `EventEnumerator<T>` | `...Concurrency.Primitives` | [📄 EventEnumerator](docs/EventEnumerator.md) |

### Helpers & Supporting Types

| Type | Namespace | Docs |
|------|-----------|------|
| `AsyncMarshal` | `Steelax.Toolkit.HighPerformance` | [📄 AsyncMarshal](docs/AsyncMarshal.md) |
| `EventEnumeratorState` / `EventTaskState` | `...Concurrency.Primitives` | [📄 Result States](docs/ResultStates.md) |
| `CompleteSignal` | `...Concurrency.Primitives` | [📄 CompleteSignal](docs/CompleteSignal.md) |

---

## 🔗 Related

- **Source**: [`Steelax.Toolkit.HighPerformance.csproj`](src/Steelax.Toolkit.HighPerformance/Steelax.Toolkit.HighPerformance.csproj)

---

## 📋 Requirements

- .NET 10.0+
- C# 13+

## 🛠️ Build & Test

```
dotnet restore
dotnet build --configuration Release
dotnet test
```

The repository is a multi-project solution:

| Project | Path | Purpose |
|---------|------|---------|
| Library | `src/Steelax.Toolkit.HighPerformance` | The package source |
| Tests | `tests/Steelax.Toolkit.HighPerformance.Tests` | xUnit v3 unit tests |
| Exploration | `tests/Steelax.Toolkit.HighPerformance.Exploration` | Behavior exploration of async enumerators / `ValueTask` |
| Benchmarks | `benchmarks/Steelax.Toolkit.HighPerformance.Benchmarks` | BenchmarkDotNet benchmarks (`dotnet run -c Release`) |

NuGet package versions are managed centrally via [`Directory.Packages.props`](Directory.Packages.props); shared build metadata (authors, license, SourceLink) lives in [`Directory.Build.props`](Directory.Build.props).

## 🚀 CI / Release

The [`main.yml`](.github/workflows/main.yml) workflow builds, runs tests, packs, and — on `v*` tags — publishes the package to NuGet. Versioning is driven by [GitVersion](GitVersion.yml) (GitHubFlow, continuous deployment).

## 📄 License

Licensed under the [MIT License](LICENSE).
