using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// A fixed-capacity, pre-allocated SPSC ring with cursor-based access: elements are created once
/// by a factory, then a window of occupied slots is grown via <c>Advance</c> and observed via
/// <c>Peek</c> with no allocations on the hot path.
/// </summary>
/// <typeparam name="T">The type of elements stored in the ring.</typeparam>
/// <remarks>
/// <para>
/// All <see cref="Capacity"/> slots are created up front by the supplied factory, but the ring
/// starts empty: the occupied window is grown by <c>AdvanceFirst</c>/<c>AdvanceLast</c>. Advancing
/// reserves one more slot (from the front or the back of the window) and returns its position;
/// when the window is empty both variants reserve the same first slot. The window can hold at most
/// <see cref="Capacity"/> slots.
/// </para>
/// <para>
/// <c>PeekFirst</c>/<c>PeekLast</c> return the position of a slot already inside the window without
/// changing it. Data access is done through the indexer <c>this[Index]</c>, which returns a
/// <see langword="ref"/> for in-place mutation. Consuming is logical: slots are never physically
/// removed (<c>RemoveFirst</c>/<c>RemoveLast</c> only shrink the window), so elements can be reset
/// and reused for the next cycle.
/// </para>
/// <para>
/// The internal buffer is circular and its length is a power of two, so ring indices are computed
/// with a bitwise mask instead of a modulo. Access is intended for a single consumer thread; do not
/// hold returned references across a mutating operation (<c>Advance*</c>/<c>Remove*</c>).
/// </para>
/// </remarks>
[PublicAPI]
public sealed class RingCursor<T>
{
    private readonly T[] _buffer;
    private readonly uint _capacity;
    private readonly uint _mask;
    private uint _count;
    private uint _head;

    /// <summary>
    /// Initializes a new <see cref="RingCursor{T}"/> and pre-allocates every slot via the factory.
    /// </summary>
    /// <param name="capacity">The maximum number of buffered elements (must be greater than 0).</param>
    /// <param name="factory">The factory used to create each slot (must not be <see langword="null"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
    [PublicAPI]
    public RingCursor(int capacity, Func<T> factory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentNullException.ThrowIfNull(factory);

        _capacity = (uint)capacity;
        _buffer = new T[NextPowerOfTwo((uint)capacity)];
        _mask = (uint)_buffer.Length - 1;

        for (var i = 0; i < _buffer.Length; i++)
            _buffer[i] = factory();
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

    /// <summary>Gets the number of elements currently occupied in the ring.</summary>
    [PublicAPI]
    public int Count => (int)_count;

    /// <summary>Indicates whether the ring contains no occupied elements.</summary>
    [PublicAPI]
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Returns the position of the element at the front of the window without changing it.
    /// </summary>
    /// <param name="offset">The position of the first element.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PeekFirst(out Index offset)
    {
        if (_count == 0)
        {
            offset = default;
            return false;
        }

        offset = new Index(0);
        return true;
    }

    /// <summary>
    /// Returns the position of the element at the back of the window without changing it.
    /// </summary>
    /// <param name="offset">The position of the last element.</param>
    /// <returns>
    /// <see langword="true"/> if an element is available; otherwise, <see langword="false"/>.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PeekLast(out Index offset)
    {
        if (_count == 0)
        {
            offset = default;
            return false;
        }

        offset = new Index((int)_count - 1);
        return true;
    }

    /// <summary>
    /// Reserves the next slot at the front of the window and returns its position: the window grows
    /// by one. When the window is empty this reserves the first slot, identical to
    /// <see cref="AdvanceLast(out Index)"/>.
    /// </summary>
    /// <param name="offset">The position of the reserved element.</param>
    /// <returns>
    /// <see langword="true"/> if a slot was reserved; <see langword="false"/> when full.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AdvanceFirst(out Index offset)
    {
        if (_count == _capacity)
        {
            offset = default;
            return false;
        }

        _head = (_head - 1) & _mask;
        _count++;
        offset = new Index(0);
        return true;
    }

    /// <summary>
    /// Reserves the next slot at the back of the window and returns its position: the window grows
    /// by one. When the window is empty this reserves the first slot, identical to
    /// <see cref="AdvanceFirst(out Index)"/>.
    /// </summary>
    /// <param name="offset">The position of the reserved element.</param>
    /// <returns>
    /// <see langword="true"/> if a slot was reserved; <see langword="false"/> when full.
    /// </returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AdvanceLast(out Index offset)
    {
        if (_count == _capacity)
        {
            offset = default;
            return false;
        }

        _count++;
        offset = new Index((int)_count - 1);
        return true;
    }

    /// <summary>
    /// Removes the element at the front of the window: the window shrinks by one without
    /// physically deleting the element.
    /// </summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ShrinkFirst()
    {
        if (_count == 0)
            return;

        _head = (_head + 1) & _mask;
        _count--;
    }

    /// <summary>
    /// Removes the element at the back of the window: the window shrinks by one without
    /// physically deleting the element.
    /// </summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ShrinkLast()
    {
        if (_count == 0)
            return;

        _count--;
    }

    /// <summary>
    /// Gets a reference to the element at the specified <paramref name="offset"/> from the front of
    /// the window, allowing in-place mutation (<c>[0]</c>/<c>[^Count]</c> is the first,
    /// <c>[^1]</c> is the last).
    /// </summary>
    /// <param name="offset">The offset from the front of the window (0..<see cref="Count"/>-1, or ^1..^<see cref="Count"/>).</param>
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

            return ref _buffer[(_head + (offset.IsFromEnd
                ? _count - (uint)offset.Value
                : (uint)offset.Value)) & _mask];
        }
    }

    /// <summary>
    /// Gets a reference to the element at the specified <paramref name="offset"/>, normalizing it
    /// cyclically by <see cref="Capacity"/> and then delegating to <c>this[Index]</c> for the
    /// window check.
    /// </summary>
    /// <remarks>
    /// The offset is mapped into <c>[0..Capacity-1]</c>: negative values and values greater than or
    /// equal to <see cref="Capacity"/> wrap around. <c>[^N]</c> is treated as
    /// <c>[Capacity - N]</c>.
    /// </remarks>
    /// <param name="offset">The offset to normalize and resolve.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the normalized offset is outside the occupied window.
    /// </exception>
    [PublicAPI]
    public ref T GetAt(Index offset)
    {
        var raw = offset.IsFromEnd
            ? (int)_capacity - offset.Value
            : offset.Value;

        var normalized = raw % (int)_capacity;
        if (normalized < 0)
            normalized += (int)_capacity;

        return ref this[normalized];
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
