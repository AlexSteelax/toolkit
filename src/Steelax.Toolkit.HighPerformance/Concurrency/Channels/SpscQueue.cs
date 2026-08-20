using System.Diagnostics.CodeAnalysis;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <inheritdoc cref="SpscCoreQueue{T}"/>
/// <remarks>
/// The synchronous leaf of the SPSC family: it forwards the core transfer operations
/// (<see cref="TryRead"/>, <see cref="TryPeek"/>, <see cref="TryWrite"/>, <see cref="TryComplete"/>) and
/// does not expose asynchronous readiness — waiting is layered only by channel types.
/// </remarks>
public sealed class SpscQueue<T>(int capacity) : SpscCoreQueue<T>(capacity)
{
    /// <inheritdoc cref="SpscCoreQueue{T}.TryRead"/>
    [PublicAPI]
    public new bool TryRead([MaybeNullWhen(false)] out T value) => base.TryRead(out value);
    
    /// <inheritdoc cref="SpscCoreQueue{T}.TryPeek"/>
    [PublicAPI]
    public new bool TryPeek([MaybeNullWhen(false)] out T value) => base.TryPeek(out value);
    
    /// <inheritdoc cref="SpscCoreQueue{T}.TryWrite"/>
    [PublicAPI]
    public new bool TryWrite(T item) => base.TryWrite(item);
    
    /// <inheritdoc cref="SpscCoreQueue{T}.TryComplete"/>
    [PublicAPI]
    public new bool TryComplete(Exception? ex = null) => base.TryComplete(ex);

    /// <inheritdoc cref="SpscCoreQueue{T}.TryTerminate"/>
    [PublicAPI]
    public new bool TryTerminate(Exception ex) => base.TryTerminate(ex);
}