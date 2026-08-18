namespace Steelax.Toolkit.HighPerformance.Concurrency.Channels;

/// <summary>
/// Write-side view of a channel: writes, waits for free capacity, and completes the stream.
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
[PublicAPI]
public readonly struct ChannelWriter<T>(SpscQueue<T> queue)
{
    /// <inheritdoc cref="SpscQueue{T}.TryWrite"/>
    public bool TryWrite(T item) => queue.TryWrite(item);
    
    /// <inheritdoc cref="SpscQueue{T}.TryComplete"/>
    public bool TryComplete(Exception? ex = null) => queue.TryComplete(ex);
    
    /// <inheritdoc cref="SpscQueue{T}.WaitToWriteAsync"/>
    public ValueTask WaitToWriteAsync() => queue.WaitToWriteAsync();
    
    /// <inheritdoc cref="SpscQueue{T}.Count"/>
    public int Count => queue.Count;
    
    /// <inheritdoc cref="SpscQueue{T}.IsWritable"/>
    public bool IsWritable => queue.IsWritable;
}
