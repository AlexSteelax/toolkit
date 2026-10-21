using System.Runtime.CompilerServices;

namespace Steelax.Toolkit.HighPerformance;

/// <summary>
///     Atomic compare-and-swap helpers for monotonically advancing numeric state.
/// </summary>
[PublicAPI]
public static class AdvancedInterlocked
{
    /// <summary>
    ///     Atomically advances <paramref name="location" /> to <c>max(location, value)</c>.
    /// </summary>
    /// <param name="location">The field to advance. Read and written with <see cref="Volatile" /> semantics.</param>
    /// <param name="value">The value to promote the field to.</param>
    /// <returns>The field's value observed by the winning operation.</returns>
    /// <remarks>
    ///     If the field already holds a value greater than or equal to <paramref name="value" />, no write
    ///     happens and the current value is returned — the caller's contribution is a no-op.
    /// </remarks>
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CompareExchangeMax(ref long location, long value)
    {
        while (true)
        {
            var comparand = Volatile.Read(ref location);

            if (comparand >= value)
                return comparand;

            var next = Interlocked.CompareExchange(ref location, value, comparand);

            if (next == comparand)
                return next;
        }
    }
}