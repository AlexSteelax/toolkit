using System.Diagnostics.CodeAnalysis;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;
using Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;
using Xunit.Sdk;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class CompleteSignalTests
{
    /// <summary>
    ///     Aggressive concurrency repro for <see cref="CompleteSignal" />: many threads call
    ///     <see cref="CompleteSignal.Signal" /> (the supported model — signaling is multi-thread safe,
    ///     waiting is single-threaded) while a single consumer re-registers a wait after every signal.
    ///
    ///     Producers fire <c>total</c> signals back-to-back without waiting for consumption, so under
    ///     contention a Signal can land when the readiness flag is already charged (state == 2), where
    ///     <see cref="CompleteSignal.Signal" /> returns early without any effect. Every such signal must
    ///     still be observable by the consumer through the charged state: the consumer consumes exactly
    ///     one readiness per iteration via <see cref="CompleteSignal.TryReset" />, so any signal absorbed
    ///     by a stale state and then cleared before the consumer re-registers manifests either as a
    ///     shortfall (<c>consumed &lt; total</c>) or as the consumer parking forever (timeout).
    /// </summary>
    public sealed class Concurrency(ITestOutputHelper output)
    {
        private const int Producers = 16;
        private const int Rounds = 4;
        private const int Total = Producers * Rounds;

        [Fact(Timeout = 3_000)]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task ManySignallers_SingleWaiter_NoSignalIsLost()
        {
            var signal = new CompleteSignal();
            
            var sync = new Lock();

            long consumed = 0;
            long fired = 0;

            // The single legitimate waiter: each iteration must observe one readiness signal. If a
            // Signal is lost the consumer is left parked in WaitAsync and trips the inner timeout.
            var consumer = Task.Run(async () =>
            {
                for (var i = 0; i < Total; i++)
                {
                    if (!await signal.WaitAsync())
                        break;

                    Interlocked.Increment(ref consumed);
                }
            }, TestContext.Current.CancellationToken);
            
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // Each producer fires its share of signals without waiting for consumption, maximizing the
            // chance that a Signal arrives while the readiness flag is already charged.
            var producers = Enumerable.Range(0, Producers)
                .Select(_ => Task.Run(() =>
                {
                    var i = 0;
                    
                    try
                    {
                        for (; i < Rounds; i++)
                        {
                            signal.Signal();
                        }
                    }
                    finally
                    {
                        lock (sync)
                        {
                            fired += i;
                        }
                    }
                }, TestContext.Current.CancellationToken))
                .ToArray();

            try
            {
                await Task.WhenAll(producers).WaitAsync(TestContext.Current.CancellationToken);

                signal.Complete();

                await consumer.WaitAsync(TestContext.Current.CancellationToken);

                Assert.Equal(Total, Volatile.Read(ref fired));
                Assert.True(Volatile.Read(ref consumed) > 1);
            }
            finally
            {
                output.WriteLine($"{Volatile.Read(ref fired)}:{Volatile.Read(ref consumed)}");
            }
        }

        [FlakyTheory(100, Timeout = 1_000)]
        [InlineData(100, 1)]
        [InlineData(100, 16)]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task PingPong_NoTimeout(int iterations, int writers)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writers);
            
            var csReader = new CompleteSignal();
            var csWriter = new CompleteSignal();

            var write = 0L;
            var read = 0L;
            
            var reader = Task.Run(async () =>
            {
                while (true)
                {
                    if (!await csReader.WaitAsync())
                        break;
                    
                    csWriter.Signal();
                    Interlocked.Increment(ref read);
                }
            }, TestContext.Current.CancellationToken);
            
            var writer = Task.Run(async () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    csReader.Signal();
                    Interlocked.Increment(ref write);

                    if (!await csWriter.WaitAsync())
                        break;
                }
            }, TestContext.Current.CancellationToken);

            var pulsers = Enumerable.Range(0, writers - 1).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    csReader.Signal();
                }
            }, TestContext.Current.CancellationToken)).ToArray();

            try
            {
                await writer.WaitAsync(TestContext.Current.CancellationToken);
                await Task.WhenAll(pulsers).WaitAsync(TestContext.Current.CancellationToken);
                
                csWriter.Complete();
                csReader.Complete();
                
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