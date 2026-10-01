namespace Steelax.Toolkit.HighPerformance.Primitives;

/// <summary>
/// Enumerates the values congruent to <c>remainder</c> modulo a step within
/// <c>[minRange..maxRange]</c> without allocation. The first matching value is located at
/// construction time, so <see cref="MoveNext(out int)"/> only compares and adds.
/// </summary>
[PublicAPI]
internal struct BitmaskSequence
{
    private readonly int _maxRange;
    private readonly int _divisor;

    private long _current;

    /// <summary>
    /// Creates a sequence stepping by an arbitrary <paramref name="divisor"/>.
    /// </summary>
    /// <param name="divisor">The step between consecutive values (must be greater than 0).</param>
    /// <param name="remainder">The residue of every value modulo <paramref name="divisor"/>
    /// (must be less than <paramref name="divisor"/>).</param>
    /// <param name="maxRange">The inclusive upper bound of the sequence.</param>
    /// <param name="minRange">The inclusive lower bound of the sequence; defaults to 0.</param>
    /// <returns>A sequence of values congruent to <paramref name="remainder"/>
    /// modulo <paramref name="divisor"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="divisor"/> is not positive, <paramref name="remainder"/> is negative,
    /// or <paramref name="remainder"/> is greater than or equal to <paramref name="divisor"/>.
    /// </exception>
    [PublicAPI]
    public static BitmaskSequence Create(int divisor, int remainder, int maxRange, int minRange = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(divisor);
        ArgumentOutOfRangeException.ThrowIfNegative(remainder);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(remainder, divisor);

        // First value congruent to remainder modulo divisor that is not less than minRange.
        var start = (long)minRange - minRange % divisor + remainder;
        if (start < minRange)
            start += divisor;

        return new BitmaskSequence(start, divisor, maxRange);
    }

    /// <summary>
    /// Creates a sequence stepping by a power of two, located with bitwise operations
    /// instead of a modulo.
    /// </summary>
    /// <param name="k">The exponent so that the step is <c>2^k</c> (0..30).</param>
    /// <param name="remainder">The residue of every value modulo <c>2^k</c>
    /// (must be less than <c>2^k</c>).</param>
    /// <param name="maxRange">The inclusive upper bound of the sequence.</param>
    /// <param name="minRange">The inclusive lower bound of the sequence; defaults to 0.</param>
    /// <returns>A sequence of values congruent to <paramref name="remainder"/> modulo <c>2^k</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="k"/> is negative or greater than 30, <paramref name="remainder"/>
    /// is negative, or <paramref name="remainder"/> is greater than or equal to <c>2^k</c>.
    /// </exception>
    [PublicAPI]
    public static BitmaskSequence CreateFromPowerOfTwo(int k, int remainder, int maxRange, int minRange = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(k);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(k, sizeof(int) * 8 - 1);

        var divisor = 1 << k;
        ArgumentOutOfRangeException.ThrowIfNegative(remainder);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(remainder, divisor);

        // Aligns minRange down to the divisor grid without a modulo:
        // (~(divisor - 1)) zeroes the low bits, then the remainder places the value in the grid cell.
        var mask = divisor - 1;
        var start = (long)(minRange & ~mask) + remainder;
        if (start < minRange)
            start += divisor;

        return new BitmaskSequence(start, divisor, maxRange);
    }

    private BitmaskSequence(long start, int divisor, int maxRange)
    {
        _current = start;
        _divisor = divisor;
        _maxRange = maxRange;
    }

    /// <summary>
    /// Advances the sequence by one step and returns the current value.
    /// </summary>
    /// <param name="value">The current value when <see langword="true"/> is returned;
    /// unspecified otherwise.</param>
    /// <returns>
    /// <see langword="true"/> while the current value does not exceed <c>maxRange</c>;
    /// <see langword="false"/> when the sequence is exhausted.
    /// </returns>
    [PublicAPI]
    public bool MoveNext(out int value)
    {
        if (_current > _maxRange)
        {
            value = 0;
            return false;
        }

        value = (int)_current;
        _current += _divisor;

        return true;
    }
}