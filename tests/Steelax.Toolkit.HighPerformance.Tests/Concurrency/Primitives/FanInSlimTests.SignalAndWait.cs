using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class SignalAndWait
    {
        [Fact]
        public async Task Signal_BeforeAndAfterWait_CompletesCorrectly()
        {
            var source = new FanInSlim();

            // Signal before wait — synchronous completion
            source.Signal(0);
            source.Signal(2);
            source.Signal(4);

            await source.WaitAsync();
            Assert.True(source.Take().Any);

            // Signal after wait — asynchronous completion
            var waitTask = source.WaitAsync();
            Assert.False(waitTask.IsCompleted);

            source.Signal(1);
            await waitTask;
            Assert.True(source.Take().Any);
        }

        [Fact]
        public async Task WaitAsync_CanBeCalledMultipleTimes()
        {
            var source = new FanInSlim();

            source.Signal(0);
            await source.WaitAsync();
            Assert.True(source.Take().Any);

            source.Signal(1);
            await source.WaitAsync();
            Assert.True(source.Take().Any);

            source.Signal(2);
            await source.WaitAsync();
            Assert.True(source.Take().Any);
        }

        [Fact]
        public async Task WaitAsync_AfterTake_WorksCorrectly()
        {
            var source = new FanInSlim();
            source.Signal(0);

            Assert.True(source.Take().Any);

            var waitTask = source.WaitAsync();
            Assert.False(waitTask.IsCompleted);

            source.Signal(1);
            await waitTask;
            Assert.True(source.Take().Any);
        }

        [Fact]
        public async Task Signal_All32Slots_ReturnsAll()
        {
            var source = new FanInSlim();

            for (var i = 0; i < 32; i++)
                source.Signal(i);

            await source.WaitAsync();
            var slots = source.Take();
            Assert.True(slots.Any);
            Assert.Equal(32, slots.Count);
        }

        [Fact]
        public async Task Signal_Indices0To4_AllAtOnceAndSequentially()
        {
            var source = new FanInSlim();

            // All at once
            for (var i = 0; i <= 4; i++)
                source.Signal(i);

            await source.WaitAsync();
            Assert.True(source.Take().Any);

            // Sequential signals
            for (var i = 0; i <= 4; i++)
            {
                source.Signal(i);
                await source.WaitAsync();
                Assert.True(source.Take().Any);
            }
        }
    }
}