using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// A set of slot indices (0..31) represented as a bitmask.
/// </summary>
/// <remarks>
/// <see cref="TryPop"/> consumes one slot at a time. All operations return a new instance;
/// value equality is provided by the compiler.
/// </remarks>
public readonly record struct SlotSet
{
    private readonly uint _slots;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SlotSet(uint slots) => _slots = slots;

    /// <summary>Creates a <see cref="SlotSet"/> from a raw bitmask.</summary>
    /// <param name="mask">A bitmask where each set bit (0..31) is a slot index.</param>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SlotSet FromMask(uint mask) => new(mask);

    /// <summary>Creates a <see cref="SlotSet"/> from slot indices.</summary>
    /// <param name="slots">The slot indices (0..31) to set.</param>
    /// <exception cref="ArgumentOutOfRangeException">A slot index is outside 0..31.</exception>
    [PublicAPI]
    public static SlotSet Of(params int[] slots)
    {
        var mask = 0u;

        foreach (var slot in slots)
        {
            ThrowIfInvalidSlot(slot);
            mask |= 1u << slot;
        }

        return new SlotSet(mask);
    }

    /// <summary>Gets the raw bitmask value.</summary>
    [PublicAPI]
    public uint Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _slots;
    }

    /// <summary>Gets a value indicating whether at least one slot is set.</summary>
    [PublicAPI]
    public bool Any
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _slots != 0;
    }

    /// <summary>Gets the number of set slots.</summary>
    [PublicAPI]
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BitOperations.PopCount(_slots);
    }

    /// <summary>Determines whether the specified slot is set.</summary>
    /// <param name="index">The slot index (0..31) to test.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSet(int index)
    {
        ThrowIfInvalidSlot(index);

        return (_slots & (1u << index)) != 0;
    }

    /// <summary>Removes and returns the lowest-indexed set slot.</summary>
    /// <param name="set">The remaining set with the popped bit cleared.</param>
    /// <param name="index">The removed slot index (only meaningful when the method returns <see langword="true"/>).</param>
    /// <returns><see langword="true"/> when a slot was removed; otherwise, <see langword="false"/>.</returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out int index, out SlotSet set)
    {
        if (_slots == 0)
        {
            set = this;
            index = 0;
            return false;
        }

        index = BitOperations.TrailingZeroCount(_slots);
        set = new SlotSet(_slots & (_slots - 1));

        return true;
    }

    /// <summary>Removes the specified slot when it is set.</summary>
    /// <param name="index">The slot index (0..31) to remove.</param>
    /// <param name="set">
    /// The resulting set with the slot removed, or the original set when the slot was not present.
    /// </param>
    /// <returns><see langword="true"/> when the slot was present and removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(int index, out SlotSet set)
    {
        ThrowIfInvalidSlot(index);

        var bit = 1u << index;
        var removed = (_slots & bit) != 0;

        set = new SlotSet(_slots & ~bit);

        return removed;
    }

    /// <summary>Returns the raw mask value followed by the set slot indices, e.g. <c>"11[0 1 3]"</c>.</summary>
    [PublicAPI]
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(_slots);
        builder.Append('[');

        var slots = this;
        var first = true;
        while (slots.TryPop(out var index, out slots))
        {

            if (!first)
                builder.Append(' ');
            builder.Append(index);

            first = false;
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static void ThrowIfInvalidSlot(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, sizeof(int) * 8);
    }
}
