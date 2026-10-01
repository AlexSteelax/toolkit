using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class FanInSlimTests
{
    public sealed class Concurrency(ITestOutputHelper output)
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
                        var wait = fanIn.WaitToReadyAsync();
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
        
        [FlakyTheory(100, Timeout = 1_000)]
        [InlineData(100, 1)]
        [InlineData(100, 8)]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task PingPong_NoTimeout(int iterations, int writers)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writers);
            
            var csReader = new FanInSlim();
            var csWriter = new FanInSlim();

            var write = 0L;
            var read = 0L;
            
            var reader = Task.Run(async () =>
            {
                while (true)
                {
                    var ret = await csReader.WaitAsync();
                    
                    if (csReader.Take().IsSet(0))
                    {
                        Interlocked.Increment(ref read);
                        csWriter.Signal(0);
                    }
                    
                    if (!ret)
                        break;
                }
                
                csWriter.Complete();
            }, TestContext.Current.CancellationToken);
            
            var writer = Task.Run(async () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    csReader.Signal(0);

                    var ret = await csWriter.WaitAsync();
                    
                    if (csWriter.Take().IsSet(0))
                        Interlocked.Increment(ref write);
                    
                    if (!ret)
                        break;
                }
                
                csReader.Complete();
            }, TestContext.Current.CancellationToken);

            var pulsers = Enumerable.Range(0, writers - 1).Select(_ => Task.Run(() =>
            {
                while(!csReader.IsCompleted)
                {
                    csReader.Signal(Random.Shared.Next(1, 31));
                }
            }, TestContext.Current.CancellationToken)).ToArray();

            try
            {
                await writer.WaitAsync(TestContext.Current.CancellationToken);
                await Task.WhenAll(pulsers).WaitAsync(TestContext.Current.CancellationToken);
                
                await reader.WaitAsync(TestContext.Current.CancellationToken);
            }
            finally
            {
                output.WriteLine($"Rid:Wid={csReader.GetHashCode()}:{csWriter.GetHashCode()}");
                output.WriteLine($"R:W={Volatile.Read(ref read)}:{Volatile.Read(ref write)}");
            }
            
            Assert.Equal(iterations, Volatile.Read(ref write));

            if (writers == 1)
                Assert.True(Volatile.Read(ref read) == iterations);
            else
                Assert.True(Volatile.Read(ref read) >= iterations);
        }
    }
}
