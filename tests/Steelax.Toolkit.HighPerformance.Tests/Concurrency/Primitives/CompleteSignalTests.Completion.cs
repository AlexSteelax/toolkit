using System.Diagnostics.CodeAnalysis;
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
        public async Task SignalThenComplete_TryReset_IsSuppressedByTerminalLatch()
        {
            var signal = new CompleteSignal();
            signal.Signal();
            signal.Complete();

            // Completion dominates the pending readiness marker everywhere: TryReset must not
            // resurrect an edge that WaitAsync already refuses to report.
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
            var tcs = new TaskCompletionSource();

            var waitTask = signal.WaitAsync();
            waitTask.GetAwaiter().UnsafeOnCompleted(() =>
            {
                continuationThreadId = Environment.CurrentManagedThreadId;
                tcs.SetResult();
            });

            var signalerThreadId = Environment.CurrentManagedThreadId;
            signal.Signal();

            await tcs.Task;
            Assert.Equal(signalerThreadId, continuationThreadId);
        }

        [FlakyFact(300)]
        [SuppressMessage("Reliability", "CA2012")]
        public async Task WaitAsync_WithAsynchronousContinuations_RunsContinuationOffSignallingThread()
        {
            var signal = new CompleteSignal(allowSynchronousContinuations: false);
            var continuationThreadId = int.MinValue;
            var tcs = new TaskCompletionSource();

            var waitTask = signal.WaitAsync();
            waitTask.GetAwaiter().UnsafeOnCompleted(() =>
            {
                continuationThreadId = Environment.CurrentManagedThreadId;
                tcs.SetResult();
            });

            // Signal from a dedicated non-thread-pool thread: the async wake is always delivered by
            // the thread pool, so the continuation can never run on the signaler's thread. Signalling
            // from the test method itself would be unreliable — its pool thread can be reused to run
            // the queued continuation once this method yields at the await below.
            var signalerThreadId = int.MinValue;
            var signaler = new Thread(() =>
            {
                signalerThreadId = Environment.CurrentManagedThreadId;
                signal.Signal();
            })
            {
                IsBackground = true
            };
            signaler.Start();
            signaler.Join();

            await tcs.Task;
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