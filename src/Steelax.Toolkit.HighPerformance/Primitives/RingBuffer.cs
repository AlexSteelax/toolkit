using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// A fixed-capacity ring buffer with FIFO semantics and random access by offset from the head.
/// </summary>
/// <typeparam name="T">The type of elements stored in the buffer.</typeparam>
/// <remarks>
/// <para>
/// Elements are added at the tail and removed from the head (strict FIFO). The internal
/// buffer is circular: slots freed by dequeue are immediately reused, so the buffer never
/// grows beyond its fixed capacity and requires no capacity-of-power-of-two.
/// </para>
/// <para>
/// Any element is exposed as a <see langword="ref"/> via the indexer <c>this[int]</c>,
/// allowing struct elements to be mutated in place (<c>0</c> is the head,
/// <c>Count - 1</c> is the tail). Access is intended for a single consumer thread; do not
/// hold returned references across <see cref="TryEnqueue"/> or <see cref="TryDequeue"/>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new <see cref="RingBuffer{T}"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The fixed number of slots (must be greater than 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not positive.
    /// </exception>
    [PublicAPI]
    public RingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new T[capacity];
    }

    /// <summary>Gets the fixed number of slots.</summary>
    [PublicAPI]
    public int Capacity => _buffer.Length;

    /// <summary>Gets the number of elements currently in the buffer.</summary>
    [PublicAPI]
    public int Count => _count;

    /// <summary>Indicates whether the buffer contains no elements.</summary>
    [PublicAPI]
    public bool IsEmpty => _count == 0;

    /// <summary>Indicates whether the buffer is full.</summary>
    [PublicAPI]
    public bool IsFull => _count == _buffer.Length;

    /// <summary>
    /// Adds an element to the tail of the buffer.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <see langword="true"/> if the element was added; <see langword="false"/> when full.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(T item)
    {
        if (_count == _buffer.Length)
            return false;

        _buffer[(_head + _count) % _buffer.Length] = item;
        _count++;
        return true;
    }

    /// <summary>
    /// Returns the element at the head without removing it.
    /// </summary>
    /// <param name="item">The head element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekHead([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[_head];
        return true;
    }

    /// <summary>
    /// Removes and returns the element at the head.
    /// </summary>
    /// <param name="item">The head element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element was removed; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[_head];
        _buffer[_head] = default!;
        _head = (_head + 1) % _buffer.Length;
        _count--;
        return true;
    }

    /// <summary>
    /// Returns the element at the tail (the write target) without removing it.
    /// </summary>
    /// <param name="item">The tail element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekTail([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[(_head + _count - 1) % _buffer.Length];
        return true;
    }

    /// <summary>
    /// Returns the element at the specified <paramref name="offset"/> from the head without
    /// removing it (<c>0</c> is the head, <c>Count - 1</c> is the tail).
    /// </summary>
    /// <param name="offset">The offset from the head (0..<see cref="Count"/>-1).</param>
    /// <param name="item">The element at the offset, or <see langword="default"/> when out of range.</param>
    /// <returns>
    /// <see langword="true"/> if the offset is valid; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAt(int offset, [MaybeNullWhen(false)] out T item)
    {
        if ((uint)offset >= (uint)_count)
        {
            item = default!;
            return false;
        }

        item = _buffer[(_head + offset) % _buffer.Length];
        return true;
    }

    /// <summary>
    /// Gets a reference to the element at the specified <paramref name="offset"/> from the head,
    /// allowing in-place mutation (<c>0</c> is the head, <c>Count - 1</c> is the tail).
    /// </summary>
    /// <param name="offset">The offset from the head (0..<see cref="Count"/>-1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is out of range.</exception>
    [PublicAPI]
    public ref T this[int offset]
    {
        get
        {
            if ((uint)offset >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return ref _buffer[(_head + offset) % _buffer.Length];
        }
    }

    /// <summary>
    /// Removes all elements and resets the buffer to its initial state.
    /// </summary>
    [PublicAPI]
    public void Clear()
    {
        if (_count == 0)
            return;

        for (var i = 0; i < _count; i++)
            _buffer[(_head + i) % _buffer.Length] = default!;

        _head = 0;
        _count = 0;
    }
}
