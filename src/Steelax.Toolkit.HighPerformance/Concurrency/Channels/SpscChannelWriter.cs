using System.Diagnostics.CodeAnalysis;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// A bounded, lock-free SPSC queue for subscription-based reading: the writer waits via
/// <see cref="WaitToWriteAsync"/>, while the reader observes data availability (or the end of the
/// stream) via the <see cref="OnReadReady"/> event.
/// </summary>
/// <inheritdoc cref="SpscCoreQueue{T}"/>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <param name="capacity">The maximum number of buffered items (must be positive).</param>
[PublicAPI]
public sealed class SpscChannelWriter<T>(int capacity) : SpscCoreQueue<T>(capacity)
{
    private readonly CompleteSignal _writerSignal = new();

    /// <summary>
    /// Raised on the writer side when a value becomes available or the stream ends, notifying the reader
    /// to re-check the queue (edge-triggered).
    /// </summary>
    [PublicAPI]
    public event Action? OnReadReady;

    /// <inheritdoc cref="SpscCoreQueue{T}.OnReadable"/>
    protected override void OnReadable() => OnReadReady?.Invoke();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnWritable"/>
    protected override void OnWritable() => _writerSignal.Signal();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnFilled"/>
    protected override void OnFilled() => _writerSignal.TryReset();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnCompleted"/>
    protected override void OnCompleted() => _writerSignal.Complete();

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
    
    /// <summary>Waits until capacity is available (writer side).</summary>
    /// <inheritdoc cref="SpscCoreQueue{T}.WaitToWriteAsync(CompleteSignal)"/>
    [PublicAPI]
    public ValueTask<bool> WaitToWriteAsync() => WaitToWriteAsync(_writerSignal);
}