using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class SpscChannelTests
{
    /// <summary>Drains the channel via the consumator API until the stream completes.</summary>
    private static async Task<List<int>> ReadAllAsync(SpscChannel<int> channel)
    {
        var result = new List<int>();

        while (true)
        {
            if (channel.TryRead(out var value))
            {
                result.Add(value);
                continue;
            }

            if (channel.IsCompleted)
                break;

            await channel.WaitToReadAsync();
        }

        return result;
    }
}
