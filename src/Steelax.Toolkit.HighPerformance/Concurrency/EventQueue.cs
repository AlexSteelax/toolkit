using System.Collections.Concurrent;
using System.Threading.Tasks.Sources;

namespace Steelax.Toolkit.HighPerformance.Concurrency;

/// <summary>
/// A bounded, event-driven SPSC buffer: the write side offers <see cref="TryWrite"/> /
/// <see cref="Complete"/>, the read side offers <c>TryRead</c> / <c>WaitToReadAsync</c> —
/// a consumator-style API (non-blocking read plus async wait).
/// </summary>
/// <typeparam name="T">The type of buffered values.</typeparam>
/// <remarks>
/// <para>
/// Single-producer/single-consumer by design: one writer drives <see cref="TryWrite"/> /
/// <see cref="Complete"/>, one reader drives <c>TryRead</c> / <c>WaitToReadAsync</c>.
/// </para>
/// <para>
/// Storage is a fixed-size circular buffer of power-of-two length (rounded up from the requested
/// capacity, which remains the strict upper bound of buffered items). Availability is modelled as a
/// pair of monotonically increasing counters: <c>WriterSeq</c> counts enqueued (and fully published)
/// items, <c>ReaderSeq</c> counts consumed ones. The writer publishes an item (<c>WriterSeq++</c>,
/// release) only after writing it into the ring, and the reader consumes only while the delta
/// <c>WriterSeq - ReaderSeq > 0</c> (empty when equal, full when the delta reaches the capacity).
/// This guarantees the consumed item is fully visible and closes the writer's enqueue→publish window.
/// The counters are <see cref="uint"/> (modular 2³² — wrap-around is natural, all checks use the
/// delta) and double as ring indices via the power-of-two mask. The single readiness core is charged
/// (<c>SetResult</c>) at most once per signal cycle and reset by the reader before registering a new
/// wait; a re-check closes the lost-wakeup window.
/// </para>
/// </remarks>
public partial class EventQueue<T> : IValueTaskSource
{
    private readonly T[] _buffer;
    private readonly uint _capacity;
    private readonly uint _mask;

    private bool _completed;
    private Exception? _exception;

    /// <summary>The read-side readiness core: wakes an awaiting reader when data arrives or the stream ends.</summary>
    private ManualResetValueTaskSourceCore<bool> _readCore;

    // 0 = Idle, 1 = Waiting, 2 = Signaled
    private int _state;

    /// <summary>The number of items enqueued and fully published by the writer (written only by the writer).</summary>
    internal uint WriterSeq;

    /// <summary>The number of items consumed by the reader (written only by the reader).</summary>
    internal uint ReaderSeq;

    /// <summary>
    /// Initializes a new bounded, event-driven single-producer/single-consumer queue.
    /// </summary>
    /// <param name="capacity">The maximum number of buffered items (must be positive).</param>
    /// <param name="allowSynchronousContinuations">
    /// When <see langword="true"/>, reader continuations may run inline on the writer's thread;
    /// when <see langword="false"/> (default), they are scheduled asynchronously to avoid stack
    /// growth and unexpected re-entrancy.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not positive.</exception>
    public EventQueue(int capacity, bool allowSynchronousContinuations = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = (uint)capacity;
        _buffer = new T[NextPowerOfTwo((uint)capacity + 1)];
        _mask = (uint)_buffer.Length - 1;

        _readCore = new ManualResetValueTaskSourceCore<bool> { RunContinuationsAsynchronously = !allowSynchronousContinuations };
    }

    /// <summary>
    /// Gets the number of items currently buffered.
    /// </summary>
    /// <remarks>
    /// A best-effort snapshot: under concurrent access it reflects the delta between the published
    /// (<see cref="WriterSeq"/>) and consumed (<see cref="ReaderSeq"/>) sequences at read time and may
    /// be momentarily off (e.g. an item written but not yet drained). It never underflows and costs no
    /// writes on the hot path — both counters already exist.
    /// </remarks>
    [PublicAPI]
    public int Count => (int)(Volatile.Read(ref WriterSeq) - Volatile.Read(ref ReaderSeq));

    /// <summary>Raised when a previously full buffer frees capacity (the producer may retry a rejected write).</summary>
    [PublicAPI]
    public event Action? OnWriteReady;

    /// <summary>Rounds <paramref name="value"/> up to the nearest power of two.</summary>
    private static int NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return (int)(value + 1);
    }
}
