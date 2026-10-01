using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class CompleteSignalTests
{
    /// <summary>
    /// Tests for the terminal-completion surface of <see cref="CompleteSignal"/>: <see cref="CompleteSignal.Complete"/>
    /// latching, direct waiter wake-up, suppression of later signals, and the continuation scheduling flag.
    /// </summary>
    public sealed class Completion
    {
        [Fact]
        public async Task Complete_WakesRegisteredWaiter_WithoutSignal()
        {
            var signal = new CompleteSignal();

            var wait = signal.WaitAsync();
            Assert.False(wait.IsCompleted);

            signal.Complete(); // no trailing Signal() is needed

            Assert.False(await wait);
        }

        [Fact]
        public async Task Complete_Twice_IsNoOp()
        {
            var signal = new CompleteSignal();

            var wait = signal.WaitAsync();
            Assert.False(wait.IsCompleted);

            signal.Complete();
            signal.Complete(); // second call is a no-op

            Assert.False(await wait);
            Assert.False(await signal.WaitAsync());
        }

        [Fact]
        public async Task WaitAsync_AfterComplete_AlwaysReturnsFalse()
        {
            var signal = new CompleteSignal();
            signal.Complete();

            for (var i = 0; i < 5; i++)
            {
                var wait = signal.WaitAsync();
                Assert.True(wait.IsCompleted);
                Assert.False(await wait);
            }
        }

        [Fact]
        public async Task SignalThenComplete_WaitAsync_ReturnsFalse()
        {
            var signal = new CompleteSignal();
            signal.Signal();
            signal.Complete();

            // Completion dominates the pending readiness marker.
            var wait = signal.WaitAsync();
            Assert.True(wait.IsCompleted);
            Assert.False(await wait);
        }

        [Fact]
        public async Task SignalThenComplete_TryReset_StillConsumesPendingReadiness()
        {
            var signal = new CompleteSignal();
            signal.Signal();
            signal.Complete();

            Assert.True(signal.TryReset());
            Assert.False(signal.TryReset());
            Assert.False(await signal.WaitAsync());
        }

        [Fact]
        public void TryReset_NoPendingSignal_ReturnsFalse()
        {
            var signal = new CompleteSignal();
            Assert.False(signal.TryReset());
        }

        [Fact]
        public async Task WaitAsync_WithSynchronousContinuations_RunsContinuationOnSignallingThread()
        {
            var signal = new CompleteSignal(); // synchronous continuations by default
            var continuationThreadId = int.MinValue;
            using var registered = new ManualResetEventSlim();
            var tcs = new TaskCompletionSource();

            var waitTask = signal.WaitAsync();

            var awaiter = Task.Run(async () =>
            {
                waitTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    continuationThreadId = Environment.CurrentManagedThreadId;
                    tcs.SetResult();
                });
                registered.Set();
                await tcs.Task;
            }, TestContext.Current.CancellationToken);

            registered.Wait(TestContext.Current.CancellationToken);

            var signalerThreadId = Environment.CurrentManagedThreadId;
            signal.Signal();

            await awaiter.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(signalerThreadId, continuationThreadId);
        }

        [Fact]
        public async Task WaitAsync_WithAsynchronousContinuations_RunsContinuationOffSignallingThread()
        {
            var signal = new CompleteSignal(allowSynchronousContinuations: false);
            var continuationThreadId = int.MinValue;
            using var registered = new ManualResetEventSlim();
            var tcs = new TaskCompletionSource();

            var waitTask = signal.WaitAsync();

            var awaiter = Task.Run(async () =>
            {
                waitTask.GetAwaiter().UnsafeOnCompleted(() =>
                {
                    continuationThreadId = Environment.CurrentManagedThreadId;
                    tcs.SetResult();
                });
                registered.Set();
                await tcs.Task;
            }, TestContext.Current.CancellationToken);

            registered.Wait(TestContext.Current.CancellationToken);

            var signalerThreadId = Environment.CurrentManagedThreadId;
            signal.Signal();

            await awaiter.WaitAsync(TestContext.Current.CancellationToken);
            Assert.NotEqual(signalerThreadId, continuationThreadId);
        }

        [Fact(Timeout = 10_000)]
        public async Task Complete_RacedWithWaitRegistration_IsNotLost()
        {
            const int attempts = 1_000;
            const int wakeTimeoutMs = 200;

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var signal = new CompleteSignal();

                var waiter = Task.Run(() => signal.WaitAsync(), TestContext.Current.CancellationToken);

                // Vary the timing: completion lands either before the registration or right inside
                // the lost-wakeup window between the state read and the waiter CAS.
                Thread.SpinWait(attempt & 0x7F);
                signal.Complete();

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
                        $"Lost wakeup reproduced on attempt {attempt}: WaitAsync registered a waiter, " +
                        $"Complete() latched the terminal bit, but the waiter was never woken.");
                }
            }
        }
    }
}