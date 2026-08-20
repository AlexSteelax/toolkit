using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static class CompleteSignalTests
{
    public sealed class Signal
    {
        [Fact]
        public async Task Signal_WakesWaiterWithTrue()
        {
            var signal = new CompleteSignal();

            var wait = signal.WaitAsync();
            Assert.False(wait.IsCompleted);

            signal.Signal();

            Assert.True(await wait);
        }

        [Fact]
        public async Task SignalBeforeWait_CompletesSynchronouslyWithTrue()
        {
            var signal = new CompleteSignal();

            signal.Signal();

            var wait = signal.WaitAsync();
            Assert.True(wait.IsCompleted);
            Assert.True(await wait);
        }

        [Fact]
        public async Task Signal_IsReusableAfterReset()
        {
            var signal = new CompleteSignal();

            signal.Signal();
            Assert.True(signal.TryReset());
            Assert.False(signal.TryReset());

            signal.Signal();
            Assert.True(await signal.WaitAsync());
        }

        [Fact]
        public async Task CompleteBeforeWait_CompletesSynchronouslyWithFalse()
        {
            var signal = new CompleteSignal();

            signal.Complete();

            var wait = signal.WaitAsync();
            Assert.True(wait.IsCompleted);
            Assert.False(await wait);
        }

        [Fact]
        public async Task CompleteThenSignal_WakesWaiterWithFalse()
        {
            var signal = new CompleteSignal();

            var wait = signal.WaitAsync();
            Assert.False(wait.IsCompleted);

            // Complete only latches the flag; Signal wakes the waiter, which observes the terminal flag.
            signal.Complete();
            signal.Signal();

            Assert.False(await wait);
        }

        [Fact]
        public async Task Complete_IsTerminal_ResetHasNoEffect()
        {
            var signal = new CompleteSignal();

            signal.Complete();

            // Completion is a one-shot latch: TryReset cannot clear it, and every wait returns false.
            Assert.False(signal.TryReset());
            Assert.False(await signal.WaitAsync());
        }

        [Fact]
        public async Task SignalAfterComplete_IsIgnored()
        {
            var signal = new CompleteSignal();

            signal.Complete();

            // A completed signal stays completed — a later readiness signal yields false.
            signal.Signal();
            Assert.False(await signal.WaitAsync());
        }
    }
}
