using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

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

            // WaitToReadAsync returns false when the stream has ended.
            if (!await channel.WaitToReadAsync())
                break;
        }

        return result;
    }
}
