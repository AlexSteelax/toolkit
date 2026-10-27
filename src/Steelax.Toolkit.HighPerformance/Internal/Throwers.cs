namespace Steelax.Toolkit.HighPerformance.Internal;

/// <summary>
/// Static precondition checks that throw exceptions when a requirement is violated.
/// </summary>
internal static class Throwers
{
    /// <summary>
    /// Throws an <see cref="ArithmeticException"/> when <paramref name="roundedUp"/> is smaller
    /// than <paramref name="requested"/>, which means the power-of-two rounding has overflowed
    /// and clamped to zero.
    /// </summary>
    /// <param name="requested">The value that was rounded up.</param>
    /// <param name="roundedUp">The power-of-two result of rounding <paramref name="requested"/>.</param>
    /// <exception cref="ArithmeticException">
    /// Thrown when <paramref name="roundedUp"/> is smaller than <paramref name="requested"/>.
    /// </exception>
    public static void ThrowIfPow2RoundUpWrapped(uint requested, uint roundedUp)
    {
        if (roundedUp < requested)
            throw new ArithmeticException($"The value {requested} cannot be rounded up to a power of two without overflowing uint");
    }
}