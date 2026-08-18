using System.Diagnostics.CodeAnalysis;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// Read-side view of a bare queue: reads and observes <see cref="Count"/> / <see cref="IsCompleted"/>.
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
[PublicAPI]
public readonly struct QueueReader<T>(SpscQueue<T> queue)
{
    /// <inheritdoc cref="SpscQueue{T}.TryRead"/>
    public bool TryRead([MaybeNullWhen(false)] out T value) => queue.TryRead(out value);
    
    /// <inheritdoc cref="SpscQueue{T}.Count"/>
    public int Count => queue.Count;
    
    /// <inheritdoc cref="SpscQueue{T}.IsReadable"/>
    public bool IsReadable => queue.IsReadable;
    
    /// <inheritdoc cref="SpscQueue{T}.IsCompleted"/>
    public bool IsCompleted => queue.IsCompleted;
}
