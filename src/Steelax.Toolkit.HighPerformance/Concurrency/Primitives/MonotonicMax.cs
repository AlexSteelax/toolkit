using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
///     A monotonic max for <see cref="long" /> that shards writer updates across cache-line-aligned slots
///     so that many writers do not contend on a single memory location. The value observed by a reader
///     never decreases.
/// </summary>
/// <remarks>
///     A writer is hashed onto its own slot (4, 8 or 16 slots; 4 slots are supported only through the
///     constructor), and a reader reduces the slots in a tree in log2(N) steps using volatile reads.
///     Writer updates advance each slot with <see cref="AdvancedInterlocked.CompareExchangeMax(ref long, long)" />,
///     so the aggregate is monotonic regardless of the number of readers and writers.
/// </remarks>
[PublicAPI]
public struct MonotonicMax
{
    private readonly int _count;  // 4, 8 or 16
    private readonly uint _mask;   // _count - 1
    private readonly int _shift;

    private SlotArray _slots;     // 16 slots allocated, _count used

    /// <summary>
    ///     Creates an instance with 8 slots initialized with <paramref name="defaultValue" />.
    /// </summary>
    /// <param name="defaultValue">
    ///     The initial value of every slot, returned by <see cref="Read()" /> before the first
    ///     <see cref="Update(long)" /> call.
    /// </param>
    /// <returns>An 8-slot <see cref="MonotonicMax" />.</returns>
    [PublicAPI]
    public static MonotonicMax Create8(long defaultValue = 0) => new(8, defaultValue);

    /// <summary>
    ///     Creates an instance with 16 slots initialized with <paramref name="defaultValue" />.
    /// </summary>
    /// <param name="defaultValue">
    ///     The initial value of every slot, returned by <see cref="Read()" /> before the first
    ///     <see cref="Update(long)" /> call.
    /// </param>
    /// <returns>A 16-slot <see cref="MonotonicMax" />.</returns>
    [PublicAPI]
    public static MonotonicMax Create16(long defaultValue = 0) => new(16, defaultValue);

    /// <summary>
    ///     Initializes a new instance of the <see cref="MonotonicMax" /> struct.
    /// </summary>
    /// <param name="slotCount">The number of slots: 4, 8 or 16.</param>
    /// <param name="defaultValue">
    ///     The initial value of every slot, returned by <see cref="Read()" /> before the first
    ///     <see cref="Update(long)" /> call.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="slotCount" /> is not 4, 8 or 16.
    /// </exception>
    public MonotonicMax(int slotCount, long defaultValue)
    {
        if (slotCount is not 4 and not 8 and not 16)
            throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "The value must be 4, 8 or 16");

        _count = slotCount;
        _mask = (uint)slotCount - 1;
        _shift = 32 - BitOperations.Log2((uint)slotCount);

        for (var i = 0; i < slotCount; i++)
            _slots[i].Value = defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SlotOf(int threadId) => (int)(unchecked((uint)threadId * 2654435761u) >> _shift) & (int)_mask;

    /// <summary>
    ///     Attempts to advance the caller's slot to <paramref name="newValue" /> if it is greater.
    ///     Safe to call from multiple threads.
    /// </summary>
    /// <param name="newValue">The value to promote the slot to.</param>
    [PublicAPI]
    public void Update(long newValue)
    {
        // Hash the current thread id with a Knuth multiplicative hash. The high bits after the
        // multiplication distribute evenly across the slots; the mask truncates to _count.
        var idx = SlotOf(Environment.CurrentManagedThreadId);

        AdvancedInterlocked.CompareExchangeMax(ref _slots[idx].Value, newValue);
    }

    /// <summary>
    ///     Reduces the slots in a tree in log2(N) steps and returns the current maximum.
    /// </summary>
    /// <returns>The maximum value across all slots.</returns>
    [PublicAPI]
    public long Read()
    {
        var span = _slots[..];

        var slotMax = _count switch
        {
            4 => Max4(span),
            8 => Max8(span),
            16 => Math.Max(Max8(span[..8]), Max8(span[8..16])),
            _ => throw new ArgumentOutOfRangeException()
        };

        return slotMax;
    }

    /// <summary>
    ///     Reads a slot value with volatile semantics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadSlot(ref PaddedSlot slot) => Volatile.Read(ref slot.Value);

    /// <summary>
    ///     Maximum of four values reduced as a tree: the critical path is two independent
    ///     <see cref="Math.Max(long, long)" /> calls plus one more.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Max4(Span<PaddedSlot> s)
    {
        var b0 = Math.Max(ReadSlot(ref s[0]), ReadSlot(ref s[1])); // independent
        var b1 = Math.Max(ReadSlot(ref s[2]), ReadSlot(ref s[3])); // independent
        return Math.Max(b0, b1); // 1 step
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Max8(Span<PaddedSlot> s)
    {
        var b0 = Max4(s[..4]);
        var b1 = Max4(s[4..8]);
        return Math.Max(b0, b1);
    }

    // 64 bytes is the cache line size on x64. Each slot occupies a separate line.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct PaddedSlot
    {
        public long Value;
    }

    [InlineArray(16)]
    private struct SlotArray
    {
        private PaddedSlot _element0;
    }
}