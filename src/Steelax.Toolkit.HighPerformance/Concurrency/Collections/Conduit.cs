using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;
using Steelax.Toolkit.HighPerformance.Internal;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Collections;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
[StructLayout(LayoutKind.Sequential)]
public partial class Conduit<T>
{
    private readonly T[] _buffer;
    private readonly uint _mask;

    private ExceptionDispatchInfo? _error;
    private bool _completed;

    // Writer's cache line: written by the writer on the hot path; read by the reader and the watchdog.
    private CacheLinePad _padWriter;
    internal uint WriterSeq;
    internal int WriterMiss;
    private int _version;

    // Reader's cache line: written by the reader on the hot path; read by the writer. _closed is
    // written by the writer only at completion, so it causes no steady-state cache-line ping-pong.
    private CacheLinePad _padReader;
    internal uint ReaderSeq;
    internal int ReaderMiss;
    private bool _closed;
    
    /// <summary>
    /// The maximum number of buffered items.
    /// </summary>
    /// <remarks>
    /// A strict upper bound: writes are rejected once the buffer holds this many items, even though the
    /// underlying ring has one extra slot (used to tell empty from full). Safe to read from either side.
    /// </remarks>
    [PublicAPI]
    public readonly uint Capacity;
    
    /// <summary>
    /// Raised on the reader side when a slot of a full buffer is freed, notifying the writer that
    /// capacity is available again (edge-triggered).
    /// </summary>
    [PublicAPI]
    public event Action? OnWriteReady;
    
    /// <summary>
    /// Raised on the writer side when a value becomes available or the stream ends, notifying the reader
    /// to re-check the queue (edge-triggered).
    /// </summary>
    [PublicAPI]
    public event Action? OnReadReady;
    
    private readonly CompleteSignal? _writerSignal;
    private readonly CompleteSignal? _readerSignal;

    private static readonly bool IsReferenceOrContainsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    /// <summary>
    /// Initializes the core with a fixed-size ring buffer.
    /// </summary>
    /// <param name="capacity">The maximum number of buffered items (must be positive).</param>
    /// <param name="behavior">The readiness behavior: selects which sides get an awaitable signal.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not positive.
    /// </exception>
    /// <remarks>
    /// Storage is rounded up to a power of two, but <see cref="Capacity"/> stays the strict upper bound
    /// of buffered items: the ring holds <c>capacity + 1</c> slots so the empty and full states remain
    /// distinguishable.
    /// </remarks>
    public Conduit(int capacity, ConduitBehavior behavior = ConduitBehavior.Default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Capacity = (uint)capacity;
        _buffer = new T[NextPowerOfTwo((uint)capacity + 1)];
        _mask = (uint)_buffer.Length - 1;
        
        if (behavior.HasFlag(ConduitBehavior.AwaitableReader))
            _readerSignal = new CompleteSignal();
        
        if (behavior.HasFlag(ConduitBehavior.AwaitableWriter))
            _writerSignal = new CompleteSignal();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
    
    private void WakeUpReader()
    {
        OnReadReady?.Invoke();
        _readerSignal?.Signal();
    }

    private void WakeUpWriter()
    {
        OnWriteReady?.Invoke();
        _writerSignal?.Signal();
    }

    // private void Close()
    // {
    //     _writerSignal?.Complete();
    //     _readerSignal?.Complete();
    // }
}