using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// A fixed-capacity double-ended queue with symmetric access to both ends and random access
/// by offset from the first element.
/// </summary>
/// <typeparam name="T">The type of elements stored in the deque.</typeparam>
/// <remarks>
/// <para>
/// Elements can be added to and removed from either end: <c>TryAddFirst</c>/<c>TryAddLast</c> add,
/// <c>TryPopFirst</c>/<c>TryPopLast</c> remove, and <c>TryPeekFirst</c>/<c>TryPeekLast</c> inspect
/// without removal. The internal storage is circular and its length is a power of two, so ring
/// indices are computed with a bitwise mask instead of a modulo, and the deque never grows beyond
/// its fixed capacity.
/// </para>
/// <para>
/// The internal buffer is rounded up to the nearest power of two and may therefore be larger than
/// <see cref="Capacity"/>: the extra slots are reserved for masking and are never exposed. The
/// capacity remains the strict upper bound of the number of buffered elements — writes are rejected
/// once <see cref="Count"/> reaches <see cref="Capacity"/>.
/// </para>
/// <para>
/// Any element is exposed as a <see langword="ref"/> via the indexer <c>this[Index]</c>, allowing
/// struct elements to be mutated in place (<c>[0]</c>/<c>[^Count]</c> is the first element,
/// <c>[^1]</c> is the last). Access is intended for a single consumer thread; do not hold returned
/// references across a mutating operation (<c>TryAdd*</c>/<c>TryPop*</c>).
/// </para>
/// </remarks>
[PublicAPI]
public sealed class Deque<T>
{
    private readonly T[] _buffer;
    private readonly uint _capacity;
    private readonly uint _mask;
    private uint _count;
    private uint _firstOffset;

    /// <summary>
    /// Initializes a new <see cref="Deque{T}"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of buffered elements (must be greater than 0).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not positive.
    /// </exception>
    [PublicAPI]
    public Deque(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = (uint)capacity;
        _buffer = new T[NextPowerOfTwo((uint)capacity)];
        _mask = (uint)_buffer.Length - 1;
    }

    /// <summary>
    /// Gets the maximum number of buffered elements.
    /// </summary>
    /// <remarks>
    /// The internal buffer is rounded up to a power of two and may contain more slots than this
    /// value; the extra slots are reserved for masking and are never exposed.
    /// </remarks>
    [PublicAPI]
    public int Capacity => (int)_capacity;

    /// <summary>Gets the number of elements currently in the deque.</summary>
    [PublicAPI]
    public int Count => (int)_count;

    /// <summary>Indicates whether the deque contains no elements.</summary>
    [PublicAPI]
    public bool IsEmpty => _count == 0;

    /// <summary>Indicates whether the deque is full.</summary>
    [PublicAPI]
    public bool IsFull => _count == _capacity;

    /// <summary>
    /// Adds an element to the front of the deque.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <see langword="true"/> if the element was added; <see langword="false"/> when full.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAddFirst(T item)
    {
        if (_count == _capacity)
            return false;

        _firstOffset = (_firstOffset - 1) & _mask;
        _buffer[_firstOffset] = item;
        _count++;
        return true;
    }

    /// <summary>
    /// Adds an element to the back of the deque.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <see langword="true"/> if the element was added; <see langword="false"/> when full.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAddLast(T item)
    {
        if (_count == _capacity)
            return false;

        _buffer[(_firstOffset + _count) & _mask] = item;
        _count++;
        return true;
    }

    /// <summary>
    /// Returns the element at the front without removing it.
    /// </summary>
    /// <param name="item">The first element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekFirst([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[_firstOffset];
        return true;
    }

    /// <summary>
    /// Returns the element at the back without removing it.
    /// </summary>
    /// <param name="item">The last element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeekLast([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[(_firstOffset + _count - 1) & _mask];
        return true;
    }

    /// <summary>
    /// Removes and returns the element at the front.
    /// </summary>
    /// <param name="item">The first element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element was removed; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPopFirst([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[_firstOffset];
        _buffer[_firstOffset] = default!;
        _firstOffset = (_firstOffset + 1) & _mask;
        _count--;
        return true;
    }

    /// <summary>
    /// Removes and returns the element at the back.
    /// </summary>
    /// <param name="item">The last element, or <see langword="default"/> when empty.</param>
    /// <returns>
    /// <see langword="true"/> if an element was removed; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPopLast([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }

        _count--;
        var lastOffset = (_firstOffset + _count) & _mask;
        item = _buffer[lastOffset];
        _buffer[lastOffset] = default!;
        return true;
    }

    /// <summary>
    /// Returns the element at the specified <paramref name="offset"/> from the first element without
    /// removing it (<c>[0]</c> is the first, <c>[^1]</c> is the last).
    /// </summary>
    /// <param name="offset">The offset from the first element (0..<see cref="Count"/>-1, or ^1..^<see cref="Count"/>).</param>
    /// <param name="item">The element at the offset, or <see langword="default"/> when out of range.</param>
    /// <returns>
    /// <see langword="true"/> if the offset is valid; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAt(Index offset, [MaybeNullWhen(false)] out T item)
    {
        if (offset.IsFromEnd
                ? (uint)offset.Value > _count
                : (uint)offset.Value >= _count)
        {
            item = default!;
            return false;
        }

        item = _buffer[(_firstOffset + (offset.IsFromEnd
            ? _count - (uint)offset.Value
            : (uint)offset.Value)) & _mask];
        return true;
    }

    /// <summary>
    /// Gets a reference to the element at the specified <paramref name="offset"/> from the first
    /// element, allowing in-place mutation (<c>[0]</c>/<c>[^Count]</c> is the first,
    /// <c>[^1]</c> is the last).
    /// </summary>
    /// <param name="offset">The offset from the first element (0..<see cref="Count"/>-1, or ^1..^<see cref="Count"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is out of range.</exception>
    [PublicAPI]
    public ref T this[Index offset]
    {
        get
        {
            if (offset.IsFromEnd
                    ? (uint)offset.Value > _count
                    : (uint)offset.Value >= _count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return ref _buffer[(_firstOffset + (offset.IsFromEnd
                ? _count - (uint)offset.Value
                : (uint)offset.Value)) & _mask];
        }
    }

    /// <summary>
    /// Removes all elements and resets the deque to its initial state.
    /// </summary>
    [PublicAPI]
    public void Clear()
    {
        if (_count == 0)
            return;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_buffer, 0, _buffer.Length);
        
        _firstOffset = 0;
        _count = 0;
    }

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
