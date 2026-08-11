using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// A set of slot indices (0..31) represented as a bitmask.
/// </summary>
/// <remarks>
/// <see cref="Pop"/> consumes one slot at a time. All operations return a new instance;
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

    /// <summary>The sentinel index returned by <see cref="Pop"/> when no slots are set.</summary>
    [PublicAPI]
    public const int None = -1;

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
    /// <param name="index">The removed slot index, or <see cref="None"/> when no slots are set.</param>
    /// <returns>The remaining set with the popped bit cleared.</returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SlotSet Pop(out int index)
    {
        if (_slots == 0)
        {
            index = None;
            return this;
        }

        index = BitOperations.TrailingZeroCount(_slots);
        var rest = _slots & (_slots - 1);

        return new SlotSet(rest);
    }

    /// <summary>Returns a new <see cref="SlotSet"/> with the specified slot removed.</summary>
    /// <param name="index">The slot index (0..31) to remove.</param>
    /// <param name="original">
    /// <see langword="true"/> when the slot was present before removal; otherwise, <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SlotSet Remove(int index, out bool original)
    {
        ThrowIfInvalidSlot(index);

        var bit = 1u << index;
        original = (_slots & bit) != 0;

        return new SlotSet(_slots & ~bit);
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
        while (slots.Any)
        {
            slots = slots.Pop(out var index);

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
