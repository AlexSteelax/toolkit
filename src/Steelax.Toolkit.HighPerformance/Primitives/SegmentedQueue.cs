using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// An unbounded, non-concurrent FIFO queue backed by a linked list of fixed-size chunks.
/// </summary>
/// <typeparam name="T">The type of buffered elements.</typeparam>
/// <remarks>
/// <para>
/// Elements are appended to a chain of chunks; each chunk is a buffer rented from a private,
/// per-<typeparamref name="T"/> array pool (<see cref="ArrayPool{T}.Create()"/>) with at least the
/// requested minimum size. The pool may grant a larger array — its full length is used as the chunk
/// capacity. When a chunk is fully drained it is detached and returned to the pool, so a steady-state
/// cycle performs no chunk allocations.
/// </para>
/// <para>
/// The type is not thread-safe: all members must be accessed from a single thread. Buffers left
/// unreturned (an abandoned non-empty queue) are reclaimed by the GC; no <c>IDisposable</c> is needed.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class SegmentedQueue<T>
{
    private static readonly bool IsReferenceOrContainsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    private static readonly ArrayPool<T> ArrayPool = ArrayPool<T>.Create();

    internal sealed class Chunk(T[] buffer)
    {
        public readonly T[] Buffer = buffer;

        public Chunk? Next;

        // write-boundary (monotonically grows up to Capacity)
        private int _count;

        // read-boundary (monotonically grows up to Count)
        private int _offset;

        public int Capacity => Buffer.Length;

        public bool IsFull => _count == Buffer.Length;

        public bool Any => _offset < _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item) => Buffer[_count++] = item;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek() => Buffer[_offset];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance()
        {
            if (IsReferenceOrContainsReferences)
                Buffer[_offset] = default!;

            _offset++;
        }
    }

    private readonly int _minimumSize;

    private Chunk? _head;
    private Chunk? _tail;

    private int _count;

    /// <summary>
    /// Initializes a new <see cref="SegmentedQueue{T}"/> with the specified minimum chunk size.
    /// </summary>
    /// <param name="minimumSize">The minimum number of slots in each chunk. Must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimumSize"/> is not positive.
    /// </exception>
    /// <remarks>
    /// Chunks are rented from a private, per-<typeparamref name="T"/> array pool
    /// (<see cref="ArrayPool{T}.Create()"/>): a chunk may be granted a larger buffer, and its full
    /// length is used as the chunk capacity.
    /// </remarks>
    [PublicAPI]
    public SegmentedQueue(int minimumSize = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumSize);

        _minimumSize = minimumSize;
    }

    /// <summary>Gets the number of elements currently buffered.</summary>
    [PublicAPI]
    public int Count => _count;

    /// <summary>Gets a value indicating whether the queue contains no elements.</summary>
    [PublicAPI]
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Adds an item to the end of the queue, allocating a new chunk when the tail is full.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <remarks>The queue is unbounded, so the operation always succeeds.</remarks>
    [PublicAPI]
    public void Enqueue(T item)
    {
        if (_tail is null)
        {
            _head = _tail = RentChunk(_minimumSize);
        }
        else if (_tail.IsFull)
        {
            var next = RentChunk(_minimumSize);
            _tail.Next = next;
            _tail = next;
        }

        _tail!.Add(item);
        _count++;
    }

    /// <summary>
    /// Removes and returns the element at the front of the queue.
    /// </summary>
    /// <param name="item">The dequeued element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element was dequeued; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// When the head chunk is fully drained it is detached from the chain and its buffer is returned
    /// to the pool for reuse.
    /// </remarks>
    [PublicAPI]
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        var head = _head;

        if (head is null)
        {
            item = default!;
            return false;
        }

        item = head.Peek();
        head.Advance();
        _count--;

        if (head.Any)
            return true;

        // The chunk is exhausted: detach it and return the buffer to the pool.
        _head = head.Next;

        if (_head is null)
            _tail = null;

        SegmentedQueue<T>.ReturnChunk(head);

        return true;
    }

    /// <summary>
    /// Returns the element at the front of the queue without removing it.
    /// </summary>
    /// <param name="item">The first element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (_head is { Any: true })
        {
            item = _head.Peek();
            return true;
        }

        item = default!;
        return false;
    }

    /// <summary>
    /// Removes all elements and returns every rented chunk buffer to the pool.
    /// </summary>
    /// <remarks>
    /// The queue can be reused after a clear; subsequent enqueues rent fresh chunks again.
    /// </remarks>
    [PublicAPI]
    public void Clear()
    {
        var chunk = _head;

        while (chunk is not null)
        {
            var next = chunk.Next;
            SegmentedQueue<T>.ReturnChunk(chunk);
            chunk = next;
        }

        _head = null;
        _tail = null;
        _count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Chunk RentChunk(int minimumSize) => new(ArrayPool.Rent(minimumSize));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnChunk(Chunk chunk) => ArrayPool.Return(chunk.Buffer, clearArray: IsReferenceOrContainsReferences);
}
