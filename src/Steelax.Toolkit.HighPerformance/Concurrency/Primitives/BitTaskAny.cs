using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// A bounded, lock-free set of task slots that signals when any tracked task completes.
/// </summary>
/// <remarks>
/// <para>
/// Slot allocation and consumption happen on a single consumer thread, while task completion
/// may occur on any thread. The signal is raised exactly when the set transitions from having
/// no completed tasks to having at least one (edge-triggered, mirroring
/// <see cref="FanInSlim"/>).
/// </para>
/// <para>
/// The caller owns task lifecycle: check <see cref="CanAdd"/> to decide whether to start work,
/// then register the running (or already completed) task via <see cref="Insert(Task)"/>, which
/// allocates a free slot and returns it. A task that completes before <see cref="Insert(Task)"/>
/// still triggers the signal synchronously, so no completion is lost.
/// </para>
/// <para>
/// After a wakeup the consumer must drain every ready task via <see cref="TryTake"/>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class BitTaskAny
{
    private readonly Action _signal;
    private readonly Task?[] _tasks;
    private readonly Action?[] _signals;
    private readonly uint _capacityMask;
    private uint _usedMask;
    private uint _readyMask;

    /// <summary>Maximum number of tracked task slots (fits a 32-bit bitmask).</summary>
    [PublicAPI]
    public const int MaxCapacity = 32;

    /// <summary>Slot index returned when no slot is available.</summary>
    [PublicAPI]
    public const int NoSlot = -1;

    /// <summary>
    /// Initializes a new <see cref="BitTaskAny"/> instance.
    /// </summary>
    /// <param name="signal">
    /// Callback invoked on the transition from no ready tasks to at least one.
    /// </param>
    /// <param name="capacity">The number of task slots (1..<see cref="MaxCapacity"/>).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="signal"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> is not within 1..<see cref="MaxCapacity"/>.
    /// </exception>
    [PublicAPI]
    public BitTaskAny(Action signal, int capacity = MaxCapacity)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, MaxCapacity);

        _signal = signal;
        _tasks = new Task[capacity];
        _signals = new Action[capacity];
        _capacityMask = uint.MaxValue >> (32 - capacity);
    }

    /// <summary>Gets the number of task slots.</summary>
    [PublicAPI]
    public int Capacity => _tasks.Length;

    /// <summary>Gets the number of currently occupied slots.</summary>
    [PublicAPI]
    public int Count => BitOperations.PopCount(_usedMask);

    /// <summary>Gets the number of completed tasks currently available.</summary>
    [PublicAPI]
    public int CountReady => BitOperations.PopCount(Volatile.Read(ref _readyMask));

    /// <summary>Indicates whether at least one completed task is available.</summary>
    [PublicAPI]
    public bool HasReady => Volatile.Read(ref _readyMask) != 0;

    /// <summary>
    /// Indicates whether at least one free slot is available for <see cref="Insert(Task)"/>.
    /// </summary>
    /// <remarks>Must be called from the consumer thread.</remarks>
    [PublicAPI]
    public bool CanAdd => (~_usedMask & _capacityMask) != 0;

    /// <summary>
    /// Registers a task (already started by the caller) into a free slot and attaches the
    /// completion signal.
    /// </summary>
    /// <param name="task">The running or already completed task to track.</param>
    /// <returns>
    /// The allocated slot, or <see cref="NoSlot"/> when the set is full.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Must be called from the consumer thread. Check <see cref="CanAdd"/> first to decide
    /// whether to start the work; if the set is full, <see cref="NoSlot"/> is returned.
    /// </para>
    /// <para>
    /// If the task has already completed, the signal fires synchronously — no completion is lost.
    /// </para>
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Insert(Task task)
    {
        var freeMask = ~_usedMask & _capacityMask;

        if (freeMask == 0)
            return NoSlot;

        var idx = BitOperations.TrailingZeroCount(freeMask);

        if (_tasks[idx] is not null)
            throw new InvalidOperationException("Insert requires a free slot.");

        _usedMask |= 1u << idx;
        _tasks[idx] = task;
        AttachSignal(task, GetOrCreateSignal(idx));

        return idx;
    }

    /// <summary>
    /// Attempts to take a completed task, freeing its slot for reuse.
    /// </summary>
    /// <param name="index">
    /// The slot of the completed task, or <see cref="NoSlot"/> when none is ready.
    /// </param>
    /// <param name="task">
    /// The completed task, or <see langword="null"/> when none is ready.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a completed task was taken; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Must be called from the consumer thread. The returned task is guaranteed completed.
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryTake(out int index, [MaybeNullWhen(false)] out Task task)
    {
        var mask = Volatile.Read(ref _readyMask);

        if (mask == 0)
        {
            index = NoSlot;
            task = null;
            return false;
        }

        var idx = BitOperations.TrailingZeroCount(mask);
        var bit = 1u << idx;

        Interlocked.And(ref _readyMask, ~bit);
        _usedMask &= ~bit;

        task = _tasks[idx];
        Debug.Assert(task is not null);
        _tasks[idx] = null;

        index = idx;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Action GetOrCreateSignal(int slot) => _signals[slot] ??= () => OnCompleted(slot);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AttachSignal(Task task, Action signal)
    {
        var awaiter = task.GetAwaiter();

        if (awaiter.IsCompleted)
            signal.Invoke();
        else
            awaiter.UnsafeOnCompleted(signal);
    }

    /// <summary>
    /// Marks the associated slot as completed, raising the signal on the 0→1 transition.
    /// </summary>
    /// <remarks>May be invoked from any thread.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void OnCompleted(int slot)
    {
        var bit = 1u << slot;

        var previous = Interlocked.Or(ref _readyMask, bit);
        if (previous == 0)
            _signal.Invoke();
    }
}
