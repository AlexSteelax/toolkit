using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

/// <summary>
///     Concurrency tests for <see cref="MonotonicMax" />.
/// </summary>
public static class MonotonicMaxTests
{
    [Fact]
    public static async Task ManyWritersManyReaders_IsMonotonicAndConvergesToMaximum()
    {
        var token = TestContext.Current.CancellationToken;
        const int writers = 32;
        const int readers = 4;
        const int count = 10_000;

        var value = MonotonicMax.Create8();

        var writerTasks = Enumerable.Range(0, writers)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 1; i <= count; i++)
                    value.Update(i);
            }, token))
            .ToArray();

        // Multiple concurrent readers: the value observed by each reader never decreases.
        var readerTasks = Enumerable.Range(0, readers)
            .Select(_ => Task.Run(() =>
            {
                var previous = 0L;

                while (writerTasks.Any(t => !t.IsCompleted))
                {
                    var current = value.Read();

                    if (current < previous)
                        throw new InvalidOperationException($"value went backwards: {previous} -> {current}");

                    previous = current;
                    Thread.Yield();
                }
            }, token))
            .ToArray();

        await Task.WhenAll(writerTasks.Concat(readerTasks));

        // The final read after all writers finish reaches the shared ceiling.
        Assert.Equal(count, value.Read());
    }

    [Fact]
    public static void Create8_ReturnsZeroInitializedMax()
    {
        var value = MonotonicMax.Create8();

        Assert.Equal(0, value.Read());
    }

    [Fact]
    public static void Create16_ReturnsZeroInitializedMax()
    {
        var value = MonotonicMax.Create16();

        Assert.Equal(0, value.Read());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public static void Constructor_SeedsSlotsWithDefaultValue(int slotCount)
    {
        var value = new MonotonicMax(slotCount, long.MaxValue);

        Assert.Equal(long.MaxValue, value.Read());
    }

    [Fact]
    public static void SeededFactory_ReturnsDefaultValueBeforeUpdates()
    {
        var value = MonotonicMax.Create16(long.MaxValue);

        Assert.Equal(long.MaxValue, value.Read());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(17)]
    public static void Constructor_RejectsUnsupportedSlotCount(int slotCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new MonotonicMax(slotCount, 0));
    }
}
