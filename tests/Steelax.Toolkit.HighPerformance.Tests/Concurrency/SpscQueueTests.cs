using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

/// <summary>
/// Unit tests for the <see cref="SpscQueue{T}"/> class.
/// </summary>
public static partial class SpscQueueTests
{
    /// <summary>Drains the queue until the stream completes.</summary>
    private static List<int> ReadAll(SpscQueue<int> queue)
    {
        var result = new List<int>();

        while (true)
        {
            if (queue.TryRead(out var value))
            {
                result.Add(value);
                continue;
            }

            if (queue.IsCompleted)
                break;

            // No data yet — yield to allow the producer to make progress.
            Thread.Yield();
        }

        return result;
    }
}
