using System.Diagnostics.CodeAnalysis;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// A bounded, lock-free SPSC queue for subscription-based writing: the reader waits via
/// <see cref="WaitToReadAsync"/>, while the writer observes freed capacity via the
/// <see cref="OnWriteReady"/> event.
/// </summary>
/// <inheritdoc cref="SpscCoreQueue{T}"/>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <param name="capacity">The maximum number of buffered items (must be positive).</param>
[PublicAPI]
public sealed class SpscChannelReader<T>(int capacity) : SpscCoreQueue<T>(capacity)
{
    private readonly CompleteSignal _readerSignal = new();

    /// <summary>
    /// Raised on the reader side when a slot of a full buffer is freed, notifying the writer that
    /// capacity is available again (edge-triggered).
    /// </summary>
    [PublicAPI]
    public event Action? OnWriteReady;

    /// <inheritdoc cref="SpscCoreQueue{T}.OnReadable"/>
    protected override void OnReadable() => _readerSignal.Signal();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnDrained"/>
    protected override void OnDrained() => _readerSignal.TryReset();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnWritable"/>
    protected override void OnWritable() => OnWriteReady?.Invoke();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnCompleted"/>
    protected override void OnCompleted() => _readerSignal.Complete();
    
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
    
    /// <summary>Waits until a value is available or the stream ends (reader side).</summary>
    /// <inheritdoc cref="SpscCoreQueue{T}.WaitToReadAsync(CompleteSignal)"/>
    [PublicAPI]
    public ValueTask<bool> WaitToReadAsync() => WaitToReadAsync(_readerSignal);
}
