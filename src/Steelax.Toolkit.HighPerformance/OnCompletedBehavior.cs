namespace Steelax.Toolkit.HighPerformance;

/// <summary>
/// Controls how <see cref="AsyncMarshal.FireUnsafeOnCompleted{T}(in System.Threading.Tasks.ValueTask{T}, System.Action?, OnCompletedBehavior)"/>
/// treats the callback when the observed operation has already completed synchronously.
/// </summary>
[PublicAPI]
public enum OnCompletedBehavior
{
    /// <summary>
    /// Invoke the callback inline when the operation is already complete (the default behavior).
    /// </summary>
    RunCallbackInline,

    /// <summary>
    /// Do not invoke the callback when the operation is already complete — the caller consumes the
    /// outcome itself (e.g. via polling). The callback is still registered for in-flight operations.
    /// </summary>
    SkipCallbackIfCompleted
}
