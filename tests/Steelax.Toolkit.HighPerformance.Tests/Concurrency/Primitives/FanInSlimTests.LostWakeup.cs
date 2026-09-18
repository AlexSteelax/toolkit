using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    /// <summary>
    ///     Stress-repro of the lost-wakeup race in <see cref="FanInSlim.WaitAsync" />.
    ///     Each capsule uses a fresh instance pre-charged to the <c>state == 2</c> (signaled but taken)
    ///     condition, then races a single <see cref="FanInSlim.Signal" /> against the waiter registration
    ///     inside <c>WaitAsync</c>. If the signal lands in the window between the mask check and the waiter
    ///     registration, the waiter is never woken while the ready bit remains set — reproducing the hang.
    /// </summary>
    public sealed class LostWakeup
    {
        [Fact(Timeout = 120_000)]
        public async Task SignalRaced_WithWaiterRegistration_IsNotLost()
        {
            const int attempts = 200_000;
            const int wakeTimeoutMs = 100;

            // Prime a few iterations before measuring: ensure nothing in the environment is cold.
            for (var i = 0; i < 100; i++)
            {
                var warm = new FanInSlim();
                warm.Signal(0);
                warm.Take(); // leaves state == 2, mask == 0

                var task = Task.Run(async () =>
                {
                    var wait = warm.WaitAsync();
                    if (!wait.IsCompleted)
                        await wait;
                    warm.Take();
                }, TestContext.Current.CancellationToken);

                warm.Signal(0);
                await task.WaitAsync(TimeSpan.FromMilliseconds(wakeTimeoutMs), TestContext.Current.CancellationToken);
            }

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var fanIn = new FanInSlim();

                // Pre-charge: a signal that has already been consumed leaves the internal state at
                // "signaled" (2) while the ready mask is empty. WaitAsync then has to walk 2 → 0 → 1
                // to register a fresh waiter, which widens the race window for a racing signal.
                fanIn.Signal(0);
                fanIn.Take();

                var waiter = Task.Run(async () =>
                {
                    var wait = fanIn.WaitAsync();
                    if (!wait.IsCompleted)
                        await wait; // May hang forever if the racing signal is lost.
                    fanIn.Take();
                }, TestContext.Current.CancellationToken);

                // Single racing signal: if it is consumed by the lost-wakeup window, no further
                // signal ever arrives to wake the registered waiter.
                fanIn.Signal(1);

                var timeout = Task.Delay(wakeTimeoutMs, TestContext.Current.CancellationToken);
                var completed = await Task.WhenAny(waiter, timeout) == waiter;

                if (!completed)
                {
                    // Wake the stuck waiter so the test process can unwind cleanly, then report.
                    fanIn.Signal(0);
                    try
                    {
                        await waiter.WaitAsync(TestContext.Current.CancellationToken);
                    }
                    catch
                    {
                        // ignore cleanup faults
                    }

                    Assert.Fail(
                        $"Lost wakeup reproduced on attempt {attempt}: WaitAsync registered a waiter, " +
                        $"Signal(1) raised the ready bit, but the waiter was never woken.");
                }
            }
        }
    }
}