using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

/// <summary>
/// Unit tests for the <see cref="SegmentedQueue{T}"/> class.
/// </summary>
public static partial class SegmentedQueueTests
{
    /// <summary>Dequeues every buffered element, preserving the FIFO order.</summary>
    private static List<T> Drain<T>(SegmentedQueue<T> queue)
    {
        var result = new List<T>();

        while (queue.TryDequeue(out var item))
            result.Add(item);

        return result;
    }
}
