using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance;

/// <summary>
/// Provides marshaling helpers for bridging standard async enumerators into the non-blocking
/// primitives used by dataflow pipelines.
/// </summary>
[PublicAPI]
public static class AsyncMarshal
{
    /// <summary>
    /// Wraps an <see cref="IAsyncEnumerator{T}"/> into a non-blocking <see cref="EventEnumerator{T}"/>
    /// that exposes readiness via an <c>OnReady</c> signal instead of awaiting <c>MoveNextAsync</c>.
    /// </summary>
    /// <typeparam name="T">The type of the enumerated elements.</typeparam>
    /// <param name="source">The async enumerator to wrap (consumed by this call).</param>
    /// <returns>An <see cref="EventEnumerator{T}"/> driving <paramref name="source"/>.</returns>
    public static EventEnumerator<T> AsNonBlocking<T>(this IAsyncEnumerator<T> source) =>
        new(source);

    /// <summary>
    /// Schedules <paramref name="callback"/> to run when <paramref name="task"/> completes, or runs it
    /// synchronously when the task is already complete.
    /// </summary>
    /// <typeparam name="T">The type of the task result.</typeparam>
    /// <param name="task">The task to observe.</param>
    /// <param name="callback">The callback to invoke when the task completes; <see langword="null"/> is a no-op.</param>
    /// <remarks>
    /// <para>
    /// Unlike a bare <c>GetAwaiter().UnsafeOnCompleted(...)</c>, which always defers a registration on an
    /// already-completed task to the thread pool, this helper runs the callback inline when the task is
    /// already complete — avoiding an unnecessary scheduling hop for synchronous completion paths.
    /// </para>
    /// <para>
    /// The callback is invoked at most once: either synchronously (the task was already completed) or once
    /// the task completes asynchronously.
    /// </para>
    /// </remarks>
    public static void FireUnsafeOnCompleted<T>(scoped in ValueTask<T> task, Action? callback)
    {
        if (callback is null)
            return;
        
        var awaiter = task.GetAwaiter();
                
        if (awaiter.IsCompleted)
            callback.Invoke();
        else
            awaiter.UnsafeOnCompleted(callback);
    }
    
    /// <summary>
    /// Schedules <paramref name="callback"/> to run when <paramref name="task"/> completes, or runs it
    /// synchronously when the task is already complete.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="callback">The callback to invoke when the task completes; <see langword="null"/> is a no-op.</param>
    /// <remarks>
    /// <para>
    /// Unlike a bare <c>GetAwaiter().UnsafeOnCompleted(...)</c>, which always defers a registration on an
    /// already-completed task to the thread pool, this helper runs the callback inline when the task is
    /// already complete — avoiding an unnecessary scheduling hop for synchronous completion paths.
    /// </para>
    /// <para>
    /// The callback is invoked at most once: either synchronously (the task was already completed) or once
    /// the task completes asynchronously.
    /// </para>
    /// </remarks>
    public static void FireUnsafeOnCompleted(scoped in ValueTask task, Action? callback)
    {
        if (callback is null)
            return;
        
        var awaiter = task.GetAwaiter();
                
        if (awaiter.IsCompleted)
            callback.Invoke();
        else
            awaiter.UnsafeOnCompleted(callback);
    }
    
    /// <summary>
    /// Schedules <paramref name="callback"/> to run when <paramref name="task"/> completes, or runs it
    /// synchronously when the task is already complete.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="callback">The callback to invoke when the task completes; <see langword="null"/> is a no-op.</param>
    /// <remarks>
    /// <para>
    /// Unlike a bare <c>GetAwaiter().UnsafeOnCompleted(...)</c>, which always defers a registration on an
    /// already-completed task to the thread pool, this helper runs the callback inline when the task is
    /// already complete — avoiding an unnecessary scheduling hop for synchronous completion paths.
    /// </para>
    /// <para>
    /// The callback is invoked at most once: either synchronously (the task was already completed) or once
    /// the task completes asynchronously.
    /// </para>
    /// </remarks>
    public static void FireUnsafeOnCompleted(scoped in Task task, Action? callback)
    {
        if (callback is null)
            return;
        
        var awaiter = task.GetAwaiter();
                
        if (awaiter.IsCompleted)
            callback.Invoke();
        else
            awaiter.UnsafeOnCompleted(callback);
    }
}
