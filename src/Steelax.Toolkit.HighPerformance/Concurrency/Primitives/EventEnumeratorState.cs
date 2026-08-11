namespace Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

/// <summary>
/// Represents the state of a single enumeration step.
/// </summary>
/// <remarks>
/// A state has exactly one of <see cref="IsPending"/>, <see cref="IsCompletedSuccessfully"/>,
/// <see cref="IsCanceled"/>, <see cref="IsFaulted"/> and <see cref="IsEndOfStream"/> set, except for
/// the default value, for which none of the flags is set. <see cref="IsCompleted"/> is a derived
/// convenience covering any completed state (success, cancellation, fault or end-of-stream).
/// </remarks>
[PublicAPI]
public readonly record struct EventEnumeratorState
{
    private readonly byte _code;

    private EventEnumeratorState(byte code) => _code = code;

    /// <summary>The operation has not completed yet.</summary>
    [PublicAPI] public static EventEnumeratorState Pending() => new(1);

    /// <summary>A value is available for the current step.</summary>
    [PublicAPI] public static EventEnumeratorState CompletedSuccessfully() => new(2);

    /// <summary>The operation was canceled.</summary>
    [PublicAPI] public static EventEnumeratorState Canceled() => new(3);

    /// <summary>The operation faulted.</summary>
    [PublicAPI] public static EventEnumeratorState Faulted() => new(4);

    /// <summary>The enumerator reached the end of the sequence.</summary>
    [PublicAPI] public static EventEnumeratorState EndOfStream() => new(5);

    /// <summary>Gets a value indicating whether the operation has not completed yet.</summary>
    [PublicAPI] public bool IsPending => _code == 1;

    /// <summary>Gets a value indicating whether a value is available for the current step.</summary>
    [PublicAPI] public bool IsCompletedSuccessfully => _code == 2;

    /// <summary>Gets a value indicating whether the operation was canceled.</summary>
    [PublicAPI] public bool IsCanceled => _code == 3;

    /// <summary>Gets a value indicating whether the operation faulted.</summary>
    [PublicAPI] public bool IsFaulted => _code == 4;

    /// <summary>Gets a value indicating whether the enumerator reached the end of the sequence.</summary>
    [PublicAPI] public bool IsEndOfStream => _code == 5;
    
    /// <summary>Gets a value indicating whether the step completed with any outcome (value, cancellation, fault or end-of-stream).</summary>
    [PublicAPI] public bool IsCompleted => IsCompletedSuccessfully || IsCanceled || IsFaulted || IsEndOfStream;

    /// <summary>
    /// Converts a state to <see cref="bool"/>, yielding <see langword="true"/> when the state is
    /// <see cref="IsCompletedSuccessfully"/>.
    /// </summary>
    /// <param name="state">The state to convert.</param>
    [PublicAPI]
    public static implicit operator bool(EventEnumeratorState state) => state.IsCompletedSuccessfully;
}