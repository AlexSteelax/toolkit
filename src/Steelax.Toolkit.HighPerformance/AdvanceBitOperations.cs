using System.Numerics;
using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance;

/// <summary>
/// Functional bit-mask operations over the 32 slot indices (0..31) packed into a <c>uint</c>.
/// </summary>
/// <remarks>
/// <para>
/// A slot mask is a plain <c>uint</c> where each set bit is a slot index. This class provides the
/// mask algebra that is not already expressed by the native bitwise operators: range masks
/// (<see cref="OfFirstBits"/>, <see cref="OfLastBits"/>), predicates
/// (<see cref="HasAny"/>, <see cref="HasAll"/>, <see cref="IsSet"/>) and
/// single-slot transitions (<c>TryPop</c>, <see cref="Set"/>, <see cref="TrySet"/>,
/// <see cref="Unset"/>, <see cref="TryUnset"/>).
/// Composition (<c>|</c>, <c>&amp;</c>, <c>^</c>, <c>~</c>) is left to the native operators.
/// Queries and factories are pure; single-slot transitions mutate the mask in place via <c>ref</c>.
/// </para>
/// </remarks>
[PublicAPI]
public static class AdvanceBitOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfInvalidSlot(int index, [CallerArgumentExpression("index")] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, sizeof(int) * 8, paramName);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfInvalidSize(int size, [CallerArgumentExpression("size")] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, sizeof(int) * 8, paramName);
    }

    /// <summary>Creates a mask with the first (lowest) <paramref name="count"/> slots set.</summary>
    /// <param name="count">The number of slots (0..32) to set.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is outside 0..32.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint OfFirstBits(int count)
    {
        ThrowIfInvalidSize(count);

        // 1u << 32 wraps to 1, so the all-32-bits case must be handled explicitly.
        return count == 32 ? uint.MaxValue : (1u << count) - 1;
    }

    /// <summary>Creates a mask with the last (highest) <paramref name="count"/> slots set.</summary>
    /// <param name="count">The number of slots (0..32) to set.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is outside 0..32.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint OfLastBits(int count)
    {
        ThrowIfInvalidSize(count);

        // 1u << 32 wraps to 1, so the no-bits case must be handled explicitly.
        return count == 0 ? 0u : ~((1u << (32 - count)) - 1);
    }

    /// <summary>Determines whether at least one slot is set in the mask.</summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasAny(uint mask)
        => mask != 0;

    /// <summary>Determines whether all 32 slots are set in the mask.</summary>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasAll(uint mask)
        => mask == uint.MaxValue;

    /// <summary>Determines whether the specified slot is set in the mask.</summary>
    /// <param name="mask">The mask to test.</param>
    /// <param name="index">The slot index (0..31) to test.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSet(uint mask, int index)
    {
        ThrowIfInvalidSlot(index);

        return (mask & (1u << index)) != 0;
    }

    /// <summary>Removes and returns the lowest-indexed set slot (hot path, no rotation).</summary>
    /// <param name="mask">The mask to pop from; the popped bit is cleared in place.</param>
    /// <param name="index">The removed slot index (only meaningful when the method returns <see langword="true"/>).</param>
    /// <returns><see langword="true"/> when a slot was removed; otherwise, <see langword="false"/>.</returns>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPop(ref uint mask, out int index)
    {
        if (mask == 0)
        {
            index = 0;
            return false;
        }

        index = BitOperations.TrailingZeroCount(mask);
        mask &= mask - 1;

        return true;
    }

    /// <summary>
    /// Removes and returns the lowest-indexed set slot at or after an offset, scanning the mask
    /// cyclically — queue semantics. <paramref name="offset"/> sets where collection starts, so
    /// successive pops with a stable offset return
    /// <c>offset, offset+1, ..., 31, 0, ..., offset-1</c>.
    /// </summary>
    /// <param name="mask">The mask to pop from; the popped bit is cleared in place.</param>
    /// <param name="index">The removed slot index (only meaningful when the method returns <see langword="true"/>).</param>
    /// <param name="offset">
    /// The slot index (0..31) at which the scan begins; <c>0</c> behaves like the plain overload.
    /// </param>
    /// <returns><see langword="true"/> when a slot was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPop(ref uint mask, out int index, int offset)
    {
        ThrowIfInvalidSlot(offset);

        if (mask == 0)
        {
            index = 0;
            return false;
        }

        // Rotate so the scan begins at `offset`, find the lowest set slot, and map it back.
        // RotateRight compiles to a single rol/ror on supported ISAs.
        var rotated = BitOperations.RotateRight(mask, offset);
        index = (BitOperations.TrailingZeroCount(rotated) + offset) & 31;
        mask &= ~(1u << index);

        return true;
    }

    /// <summary>Removes the specified slot when it is set.</summary>
    /// <param name="mask">The mask to remove from; the slot is cleared in place when present.</param>
    /// <param name="index">The slot index (0..31) to remove.</param>
    /// <returns><see langword="true"/> when the slot was present and removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnset(ref uint mask, int index)
    {
        ThrowIfInvalidSlot(index);

        var bit = 1u << index;
        var removed = (mask & bit) != 0;

        mask &= ~bit;

        return removed;
    }

    /// <summary>Removes the specified slot (idempotently).</summary>
    /// <param name="mask">The mask to remove from; the slot is cleared in place.</param>
    /// <param name="index">The slot index (0..31) to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Unset(ref uint mask, int index)
    {
        ThrowIfInvalidSlot(index);

        mask &= ~(1u << index);
    }

    /// <summary>Sets the specified slot when it is not already set.</summary>
    /// <param name="mask">The mask to add to; the slot is set in place.</param>
    /// <param name="index">The slot index (0..31) to set.</param>
    /// <returns><see langword="true"/> when the slot was absent and is now set; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySet(ref uint mask, int index)
    {
        ThrowIfInvalidSlot(index);

        var bit = 1u << index;
        var added = (mask & bit) == 0;

        mask |= bit;

        return added;
    }

    /// <summary>Sets the specified slot (idempotently).</summary>
    /// <param name="mask">The mask to add to; the slot is set in place.</param>
    /// <param name="index">The slot index (0..31) to set.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..31.</exception>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(ref uint mask, int index)
    {
        ThrowIfInvalidSlot(index);

        mask |= 1u << index;
    }
}