using Steelax.Toolkit.HighPerformance.Concurrency.Channels;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Channels;

public static partial class SpscChannelTests
{
    /// <summary>Signal-layer tests that distinguish <see cref="SpscChannel{T}"/> from its core
    /// <see cref="SpscQueue{T}"/>: waiting for read/write readiness and the ValueTask contract.</summary>
    public sealed class Functional
    {
        [Fact]
        public async Task ConsumerWaitsUntilData_ThenReads()
        {
            var channel = new SpscChannel<int>(4);

            var wait = channel.WaitToReadAsync();
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted); // no data yet — still pending

            Assert.True(channel.TryWrite(42));

            await wait;

            Assert.True(channel.TryRead(out var value));
            Assert.Equal(42, value);
        }

        [Fact]
        public async Task WriterWaitsUntilCapacity_ThenWrites()
        {
            var channel = new SpscChannel<int>(1);
            Assert.True(channel.TryWrite(1));

            // Buffer is full — the writer must wait for a slot to free.
            var wait = channel.WaitToWriteAsync();
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted); // still full — pending

            Assert.True(channel.TryRead(out var first));
            Assert.Equal(1, first);

            await wait;

            Assert.True(channel.TryWrite(2));
            Assert.True(channel.TryRead(out var second));
            Assert.Equal(2, second);
        }

        [Fact]
        public async Task Complete_EndOfStreamWakesReader()
        {
            var channel = new SpscChannel<int>(4);

            var wait = channel.WaitToReadAsync();
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.True(channel.TryComplete());

            // The reader wakes with false — the stream has ended.
            Assert.False(await wait);

            Assert.False(channel.TryRead(out _));
            Assert.True(channel.IsCompleted);
        }

        [Fact]
        public async Task WaitToReadAsync_AlreadyCompleted_ReturnsFalse()
        {
            var channel = new SpscChannel<int>(4);
            channel.TryComplete();

            Assert.False(await channel.WaitToReadAsync());
        }

        [Fact]
        public async Task WaitToWriteAsync_AlreadyCompleted_ReturnsFalse()
        {
            var channel = new SpscChannel<int>(4);
            channel.TryComplete();

            Assert.False(await channel.WaitToWriteAsync());
        }

        [Fact]
        public async Task WaitToReadAsync_DataAvailable_ReturnsTrue()
        {
            var channel = new SpscChannel<int>(4);
            Assert.True(channel.TryWrite(1));

            Assert.True(await channel.WaitToReadAsync());
        }

        [Fact]
        public async Task CompleteAfterData_WakesReaderToDrain_RestIsCoreTested()
        {
            // Ensures TryComplete signals even when data is still buffered (the signal-layer behaviour
            // layered on top of the core terminal logic already covered by SpscQueue tests).
            var channel = new SpscChannel<int>(4);
            Assert.True(channel.TryWrite(1));
            Assert.True(channel.TryWrite(2));

            channel.TryComplete();

            var collected = await ReadAllAsync(channel);

            Assert.Equal(new[] { 1, 2 }, collected);
            Assert.True(channel.IsCompleted);
        }

    }
}
