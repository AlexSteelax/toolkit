using System.Collections.Concurrent;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class Concurrency
    {
        [Fact(Timeout = 5000)]
        public async Task MultipleRecurringTimers_BlackBox_AllSlotsFire()
        {
            var fanIn = new FanInSlim();
            using var cts = new CancellationTokenSource();

            const int producerCount = 8;
            var fired = new long[producerCount];
            var iterations = 0L;
            var faults = new ConcurrentQueue<Exception>();

            // Несколько производителей на разных потоках: каждый сигналит свой слот в цикле,
            // имитируя периодические события без Timer.
            var producers = Enumerable.Range(0, producerCount)
                .Select(slot => Task.Run(() =>
                {
                    var spin = new SpinWait();

                    while (!cts.IsCancellationRequested)
                    {
                        fanIn.Signal(slot);
                        Interlocked.Increment(ref fired[slot]);
                        spin.SpinOnce();
                    }
                }, TestContext.Current.CancellationToken))
                .ToArray();

            var consumer = Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        var wait = fanIn.WaitAsync();
                        if (!wait.IsCompleted)
                            await wait;

                        var slots = fanIn.Take();
                        if (slots.Mask == 0)
                            continue;

                        Interlocked.Increment(ref iterations);

                        for (var slot = 0; slot < producerCount; slot++)
                        {
                            if (slots.IsSet(slot))
                                Interlocked.Increment(ref fired[slot]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    faults.Enqueue(ex);
                }
            }, TestContext.Current.CancellationToken);

            await Task.Delay(TimeSpan.FromMilliseconds(2000), TestContext.Current.CancellationToken);
            await cts.CancelAsync();

            // Разбудить возможное ожидание и дать потребителю завершиться.
            for (var slot = 0; slot < producerCount; slot++)
                fanIn.Signal(slot);

            // Если FanInSlim «забыл» разбудить — WaitAsync бросит TimeoutException.
            await Task.WhenAll(producers);
            await consumer.WaitAsync(TestContext.Current.CancellationToken);

            Assert.True(faults.IsEmpty, string.Join(Environment.NewLine, faults));
            Assert.True(Volatile.Read(ref iterations) > 0);

            for (var slot = 0; slot < producerCount; slot++)
                Assert.True(Volatile.Read(ref fired[slot]) > 0, $"Slot {slot} never fired");
        }
    }
}
