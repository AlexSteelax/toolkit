using System.Diagnostics.CodeAnalysis;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// A bounded, lock-free SPSC channel: an <see cref="SpscCoreQueue{T}"/> extended with asynchronous
/// read/write readiness (<see cref="WaitToReadAsync"/> / <see cref="WaitToWriteAsync"/>).
/// </summary>
/// <inheritdoc cref="SpscCoreQueue{T}"/>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <param name="capacity">The maximum number of buffered items (must be positive).</param>
[PublicAPI]
public sealed class SpscChannel<T>(int capacity) : SpscCoreQueue<T>(capacity)
{
    private readonly CompleteSignal _readerSignal = new();
    private readonly CompleteSignal _writerSignal = new();

    /// <inheritdoc cref="SpscCoreQueue{T}.OnReadable"/>
    protected override void OnReadable() => _readerSignal.Signal();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnDrained"/>
    protected override void OnDrained() => _readerSignal.TryReset();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnWritable"/>
    protected override void OnWritable() => _writerSignal.Signal();
    /// <inheritdoc cref="SpscCoreQueue{T}.OnFilled"/>
    protected override void OnFilled() => _writerSignal.TryReset();

    /// <inheritdoc cref="SpscCoreQueue{T}.OnCompleted"/>
    protected override void OnCompleted()
    {
        _writerSignal.Complete();
        _readerSignal.Complete();
    }

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
    
    /// <summary>Waits until capacity is available (writer side).</summary>
    /// <inheritdoc cref="SpscCoreQueue{T}.WaitToWriteAsync(CompleteSignal)"/>
    [PublicAPI]
    public ValueTask<bool> WaitToWriteAsync() => WaitToWriteAsync(_writerSignal);
}