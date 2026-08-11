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
}
