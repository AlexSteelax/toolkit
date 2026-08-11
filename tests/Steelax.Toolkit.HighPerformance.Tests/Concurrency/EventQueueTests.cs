using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class EventQueueTests
{
    /// <summary>Drains the queue via the consumator API until the stream completes.</summary>
    private static async Task<List<int>> ReadAllAsync(EventQueue<int> queue)
    {
        var result = new List<int>();

        while (true)
        {
            if (queue.TryRead(out var value, out var completed))
            {
                result.Add(value);
                continue;
            }

            if (completed)
                break;
            
            await queue.WaitToReadAsync();
        }

        return result;
    }
}
