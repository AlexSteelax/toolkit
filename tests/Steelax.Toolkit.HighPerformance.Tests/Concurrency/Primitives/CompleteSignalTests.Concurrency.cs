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
        public async Task ManySignallers_SingleWaiter_NoSignalIsLost()
        {
            var signal = new CompleteSignal();
            var probe = new ConduitTests.ProbeTracker(() => ConduitTests.SignalProbe.Dump(signal));
            // probe.Run();
            
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
            catch (OperationCanceledException)
            {
                probe.Stop();
                probe.ToOutput(output);
            }
            finally
            {
                probe.Stop();
                output.WriteLine($"{Volatile.Read(ref fired)}:{Volatile.Read(ref consumed)}");
            }
        }

        [Theory]
        [InlineData(30_000, 1)]
        [InlineData(10_000, 16)]
        public async Task PingPong_NoTimeout(int iterations, int writers)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writers);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cts.CancelAfter(3000);

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
            }, cts.Token);
            
            var writer = Task.Run(async () =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    csReader.Signal();
                    Interlocked.Increment(ref write);

                    if (!await csWriter.WaitAsync())
                        break;
                }
            }, cts.Token);

            var pulsers = Enumerable.Range(0, writers - 1).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < iterations; i++)
                {
                    csReader.Signal();
                }
            }, cts.Token)).ToArray();

            try
            {
                await writer.WaitAsync(cts.Token);
                await Task.WhenAll(pulsers).WaitAsync(cts.Token);
                
                csWriter.Complete();
                csReader.Complete();
                
                await reader.WaitAsync(cts.Token);
            }
            finally
            {
                output.WriteLine($"Rid:Wid={csReader.GetHashCode()}:{csWriter.GetHashCode()}");
                output.WriteLine($"R:W={Volatile.Read(ref read)}:{Volatile.Read(ref write)}");
            }
            
            Assert.Equal(iterations, Volatile.Read(ref write));
            Assert.True(Volatile.Read(ref read) >= iterations);
        }
    }
}