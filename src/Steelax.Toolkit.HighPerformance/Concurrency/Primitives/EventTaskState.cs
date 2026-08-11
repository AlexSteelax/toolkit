namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// Represents the state of an observed <see cref="EventTask{T}"/>.
/// </summary>
/// <remarks>
/// A state has exactly one of <see cref="IsPending"/>, <see cref="IsCompletedSuccessfully"/>,
/// <see cref="IsCanceled"/> and <see cref="IsFaulted"/> set, except for the default value, for which
/// none of the flags is set. <see cref="IsCompleted"/> is a derived convenience covering any completed
/// state (success, cancellation or fault).
/// </remarks>
[PublicAPI]
public readonly record struct EventTaskState
{
    private readonly byte _code;

    private EventTaskState(byte code) => _code = code;

    /// <summary>The task has not completed yet.</summary>
    [PublicAPI] public static EventTaskState Pending() => new(1);

    /// <summary>The task completed with a value.</summary>
    [PublicAPI] public static EventTaskState CompletedSuccessfully() => new(2);

    /// <summary>The task was canceled.</summary>
    [PublicAPI] public static EventTaskState Canceled() => new(3);

    /// <summary>The task faulted.</summary>
    [PublicAPI] public static EventTaskState Faulted() => new(4);

    /// <summary>Gets a value indicating whether the task has not completed yet.</summary>
    [PublicAPI] public bool IsPending => _code == 1;

    /// <summary>Gets a value indicating whether the task completed with a value.</summary>
    [PublicAPI] public bool IsCompletedSuccessfully => _code == 2;

    /// <summary>Gets a value indicating whether the task was canceled.</summary>
    [PublicAPI] public bool IsCanceled => _code == 3;

    /// <summary>Gets a value indicating whether the task faulted.</summary>
    [PublicAPI] public bool IsFaulted => _code == 4;
    
    /// <summary>Gets a value indicating whether the task completed with any outcome (value, cancellation or fault).</summary>
    [PublicAPI] public bool IsCompleted => IsCompletedSuccessfully || IsCanceled || IsFaulted;

    /// <summary>
    /// Converts a state to <see cref="bool"/>, yielding <see langword="true"/> when the state is
    /// <see cref="IsCompletedSuccessfully"/>.
    /// </summary>
    /// <param name="state">The state to convert.</param>
    [PublicAPI]
    public static implicit operator bool(EventTaskState state) => state.IsCompletedSuccessfully;
}
