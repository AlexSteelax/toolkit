using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

/// <summary>
/// Unit tests for the <see cref="EventTask{T}"/> and <see cref="EventTask"/> classes.
/// </summary>
public static partial class EventTaskTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition())
            await Task.Delay(10, token);
    }
}
