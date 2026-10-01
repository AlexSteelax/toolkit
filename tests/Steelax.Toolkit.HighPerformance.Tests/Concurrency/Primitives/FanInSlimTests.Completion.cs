using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    /// <summary>
    /// Tests for the terminal-completion surface of <see cref="FanInSlim"/>: <see cref="FanInSlim.Complete"/>,
    /// <see cref="FanInSlim.IsCompleted"/> and <see cref="FanInSlim.WaitAsync"/>, including the
    /// precedence of a pending slot over completion and the drainability of leftovers after completion.
    /// </summary>
    public sealed class Completion
    {
        [Fact]
        public async Task WaitToReadyAsync_SignalBeforeWait_ReturnsTrueSynchronously()
        {
            var source = new FanInSlim();
            source.Signal(3);

            var wait = source.WaitAsync();
            Assert.True(wait.IsCompleted);
            Assert.True(await wait);
            Assert.True(source.Take().IsSet(3));
        }

        [Fact]
        public async Task WaitToReadyAsync_SignalWakesRegisteredWaiter_WithTrue()
        {
            var source = new FanInSlim();

            var wait = source.WaitAsync();
            Assert.False(wait.IsCompleted);

            source.Signal(4);

            Assert.True(await wait);
            Assert.True(source.Take().IsSet(4));
        }

        [Fact]
        public async Task WaitToReadyAsync_CompleteBeforeWait_ReturnsFalseSynchronously()
        {
            var source = new FanInSlim();
            source.Complete();

            var wait = source.WaitAsync();
            Assert.True(wait.IsCompleted);
            Assert.False(await wait);
        }

        [Fact]
        public async Task WaitToReadyAsync_CompleteWakesRegisteredWaiter_WithFalse()
        {
            var source = new FanInSlim();

            var wait = source.WaitAsync();
            Assert.False(wait.IsCompleted);

            source.Complete();

            Assert.False(await wait);
        }

        [Fact]
        public async Task WaitToReadyAsync_SignalThenComplete_CompletedDominates_SlotStillDrainable()
        {
            var source = new FanInSlim();
            source.Signal(1);
            source.Complete();

            // Completion dominates the wait outcome; the leftover slot is still drainable via Take.
            Assert.False(await source.WaitAsync());
            Assert.True(source.Take().IsSet(1));
        }

        [Fact]
        public async Task WaitToReadyAsync_CompleteThenSignal_NothingToConsume()
        {
            var source = new FanInSlim();
            source.Complete();
            source.Signal(2); // ignored after completion

            Assert.False(await source.WaitAsync());
            Assert.False(source.Take().Any);
        }

        [Fact]
        public async Task Complete_PendingSlotsRemainDrainable_LateSignalsIgnored()
        {
            var source = new FanInSlim();
            source.Signal(0);
            source.Complete();
            source.Signal(5); // ignored: completion already latched

            var slots = source.Take();
            Assert.True(slots.IsSet(0));
            Assert.False(slots.IsSet(5));
            Assert.False(await source.WaitAsync());
        }

        [Fact]
        public async Task WaitToReadyAsync_SignalThenComplete_ReturnsFalse_LeftoverDrainable()
        {
            var source = new FanInSlim();
            source.Signal(7);
            source.Complete();

            // Completed first: no event is reported, even though the mask holds an un-drained slot.
            Assert.False(await source.WaitAsync());
            Assert.True(source.Take().IsSet(7));

            Assert.False(await source.WaitAsync()); // still completed → false
        }

        [Fact]
        public async Task WaitAsync_AfterComplete_CompletesSynchronously()
        {
            var source = new FanInSlim();
            source.Complete();

            var wait = source.WaitToReadyAsync();
            Assert.True(wait.IsCompleted);
            await wait;
        }

        [Fact]
        public async Task Complete_IsIdempotent()
        {
            var source = new FanInSlim();

            source.Complete();
            source.Complete(); // no-op

            Assert.True(source.IsCompleted);
            Assert.False(await source.WaitAsync());
        }

        [Fact]
        public void IsCompleted_IsFalseInitiallyAndTrueAfterComplete()
        {
            var source = new FanInSlim();
            Assert.False(source.IsCompleted);

            source.Complete();
            Assert.True(source.IsCompleted);
        }

        [Fact]
        public void GetSignalCallback_AfterComplete_FireIsIgnored()
        {
            var source = new FanInSlim();
            source.Complete();

            source.GetSignalCallback(9).Fire();

            Assert.False(source.Take().Any);
        }

        [Fact(Timeout = 10_000)]
        public async Task Complete_RacedWithWaitRegistration_IsNotLost()
        {
            const int attempts = 1_000;
            const int wakeTimeoutMs = 200;

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var fanIn = new FanInSlim();

                var waiter = Task.Run(() => fanIn.WaitAsync(), TestContext.Current.CancellationToken);

                // Vary the timing: completion lands either before the registration or right inside
                // the lost-wakeup window between the mask check and the waiter CAS.
                Thread.SpinWait(attempt & 0x7F);
                fanIn.Complete();

                try
                {
                    var result = await waiter.WaitAsync(
                        TimeSpan.FromMilliseconds(wakeTimeoutMs),
                        TestContext.Current.CancellationToken);
                    await result;
                }
                catch (TimeoutException)
                {
                    Assert.Fail(
                        $"Lost wakeup reproduced on attempt {attempt}: WaitToReadyAsync registered a " +
                        $"waiter, Complete() latched the terminal bit, but the waiter was never woken.");
                }
            }
        }
    }
}