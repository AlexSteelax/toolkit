# ValueTask `UnsafeOnCompleted` on an already-completed task

> Project: `Steelax.Toolkit.HighPerformance.Exploration`
> Source: [`ValueTaskBehaviorTests.cs`](ValueTaskBehaviorTests.cs)

## Summary

| Backend | Registration timing | Runs inline? | Continuation runs? | When / where | Result accessible after? | Test |
|---------|--------------------|:------------:|:------------------:|--------------|:------------------------:|------|
| Custom `IValueTaskSource<int>` (`ManualResetValueTaskSourceCore`, `RunContinuationsAsynchronously = false`) | **before** `SetResult` | yes | yes | synchronously on the `SetResult` thread | — (not yet completed) | [`ManualSource_Consumption`](ValueTaskBehaviorTests.cs) |
| Custom `IValueTaskSource<int>` (`ManualResetValueTaskSourceCore`, `RunContinuationsAsynchronously = false`) | **after** `SetResult` (already completed) | no | yes | asynchronously (thread pool) | yes (`IsCompleted`, `Result`) | [`ManualSource_OnCompleted_AlreadyCompleted`](ValueTaskBehaviorTests.cs) |
| Custom `IValueTaskSource<int>` (`ManualResetValueTaskSourceCore`, `RunContinuationsAsynchronously = false`) | **after** `SetException` (already faulted) | no | yes | asynchronously (thread pool) | yes (`IsCompleted`, `IsFaulted`, exception) | [`ManualSource_OnCompleted_ExceptionThrow`](ValueTaskBehaviorTests.cs) |
| `Task<int>`-backed (`Task.FromResult`) | **after** completion | no | yes | asynchronously (thread pool) | yes (`IsCompleted`, `Result`) | [`TaskBacked_OnCompleted_AlreadyCompleted`](ValueTaskBehaviorTests.cs) |
| `Task<int>`-backed (`Task.FromException<int>`) | **after** fault | no | yes | asynchronously (thread pool) | yes (`IsCompleted`, `IsFaulted`, exception) | [`TaskBacked_OnCompleted_AlreadyFaulted`](ValueTaskBehaviorTests.cs) |
| `default ValueTask` (no source, always "completed") | any time | no | yes | asynchronously (thread pool) | yes (`IsCompleted`, `IsCompletedSuccessfully`) | [`Default_OnCompleted_AlreadyCompleted`](ValueTaskBehaviorTests.cs) |
| Custom `IValueTaskSource<int>`, re-registration (2nd `UnsafeOnCompleted`) on an already-completed source | after completion | no | yes — **only the last registered** | asynchronously (thread pool) | yes — only until first `Result` consumption | [`ManualSource_OnCompleted_AlreadyCompleted_Twice`](ValueTaskBehaviorTests.cs) |

## Key findings

1. **`RunContinuationsAsynchronously = false` only affects the "register → complete" ordering.** When `SetResult`/`SetException` is called *after* `UnsafeOnCompleted`, the continuation runs inline on the caller's thread. When the source is *already* completed at registration time, the continuation is never run inline — it is queued and executed asynchronously.
2. **Task-backed (`TaskAwaiter`) always queues the continuation** for an already-completed task; it never runs inline, regardless of success or fault.
3. **`default ValueTask` also does not run the continuation inline**, contrary to what its "trivially completed" state might suggest — `ValueTaskAwaiter` still defers it to the thread pool.
4. **Re-registration on an already-completed custom source overwrites the previous continuation** — only the last registered one executes.
5. **Custom `IValueTaskSource` is single-consumption**: accessing `Result` a second time after the first consumption throws `InvalidOperationException`. Task-backed and default `ValueTask` can be consumed repeatedly.
