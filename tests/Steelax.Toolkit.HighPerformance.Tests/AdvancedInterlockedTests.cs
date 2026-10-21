namespace Steelax.Toolkit.HighPerformance.Tests;

/// <summary>
///     Concurrency tests for <see cref="AdvancedInterlocked.CompareExchangeMax" />.
/// </summary>
public static class AdvancedInterlockedTests
{
    [Fact]
    public static async Task ConcurrentAdvanceMax_IsMonotonicAndEndsAtMaximum()
    {
        var token = TestContext.Current.CancellationToken;
        const int count = 10_000;
        long value = 0;

        // Both writers submit monotonically increasing values [1, count]; the winning order is
        // irrelevant because each CAS can only ever promote the field upward.
        var writer1 = Task.Run(() =>
        {
            for (var i = 1; i <= count; i++)
                AdvancedInterlocked.CompareExchangeMax(ref value, i);
        }, token);

        var writer2 = Task.Run(() =>
        {
            for (var i = 1; i <= count; i++)
                AdvancedInterlocked.CompareExchangeMax(ref value, i);
        }, token);

        // A reader verifies that the value never goes backwards between its own reads.
        var reader = Task.Run(() =>
        {
            var previous = Volatile.Read(ref value);

            while (!writer1.IsCompleted || !writer2.IsCompleted)
            {
                var current = Volatile.Read(ref value);

                if (current < previous)
                    throw new InvalidOperationException($"value went backwards: {previous} -> {current}");

                previous = current;
                Thread.Yield();
            }
        }, token);

        await Task.WhenAll(writer1, writer2, reader);
        
        Assert.True(writer1.IsCompletedSuccessfully);
        Assert.True(writer2.IsCompletedSuccessfully);
        Assert.True(reader.IsCompletedSuccessfully);

        // Both writers converge on the same ceiling, so the final value is exactly `count`.
        Assert.Equal(count, Volatile.Read(ref value));
    }
}
