using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

/// <summary>
/// Unit tests for the <see cref="BitmaskSequence"/> struct.
/// </summary>
public static partial class BitmaskSequenceTests
{
    /// <summary>Drains the remaining values of the sequence into a list.</summary>
    private static int[] Collect(ref BitmaskSequence sequence)
    {
        var values = new List<int>();

        while (sequence.MoveNext(out var value))
            values.Add(value);

        return values.ToArray();
    }
}