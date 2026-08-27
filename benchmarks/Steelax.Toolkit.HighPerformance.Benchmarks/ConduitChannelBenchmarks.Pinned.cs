using System.Diagnostics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

public partial class ConduitChannelBenchmarks
{
    /// <summary>
    /// The same producer/consumer pattern as <see cref="Conduit"/>, but with the two sides' start
    /// points pinned to <em>different</em> logical processors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This variant exists to expose the effect of cache-line separation on the sequence counters of
    /// <see cref="Conduit{T}"/>. BenchmarkDotNet affinitizes the benchmark process to a single core
    /// by default, so the plain <see cref="Conduit"/> benchmark never runs the two sides in parallel
    /// and no cross-core cache-line traffic occurs; this benchmark temporarily widens the process
    /// affinity and pins each side's thread so the writer and the reader operate from distinct cores.
    /// </para>
    /// <para>
    /// Unlike a spin-based variant, no busy loops are used: the producer and the consumer await readiness
    /// exactly like <see cref="Conduit"/> does. Readiness continuations run inline on the signaling
    /// thread, so the steady-state work ping-pongs between the two pinned cores — which is precisely the
    /// traffic the counter padding targets.
    /// </para>
    /// <para>
    /// Pinning is best-effort (Windows only): if the core or the platform does not allow it, the side
    /// simply runs unpinned instead of failing. The producer targets logical processor 0 and the consumer
    /// processor <c>ProcessorCount / 2</c> (picked to avoid hyperthread siblings).
    /// </para>
    /// </remarks>
    //[Benchmark(OperationsPerInvoke = Count)]
    public async Task ConduitPinned()
    {
        var producerCore = 0;
        var consumerCore = Environment.ProcessorCount / 2;

        if (producerCore == consumerCore)
        {
            // Fewer than two logical processors — nothing to pin, run unpinned.
            await RunConcurrent(new Conduit<int>(Capacity, ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter), -1, -1);
            return;
        }

        var previousAffinity = ExpandProcessAffinity();

        try
        {
            await RunConcurrent(new Conduit<int>(Capacity, ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter), producerCore, consumerCore);
        }
        finally
        {
            RestoreProcessAffinity(previousAffinity);
        }
    }

    private static async Task RunConcurrent(Conduit<int> queue, int producerCore, int consumerCore)
    {
        var producer = Task.Factory.StartNew(async () =>
        {
            PinToCore(producerCore);

            for (var i = 0; i < Count; i++)
            {
                while (!queue.TryWrite(i))
                    await queue.WaitToWriteAsync();
            }

            queue.TryComplete();
        }, TaskCreationOptions.LongRunning).Unwrap();

        var consumer = Task.Factory.StartNew(async () =>
        {
            PinToCore(consumerCore);

            while (true)
            {
                if (queue.TryRead(out _))
                    continue;

                if (!await queue.WaitToReadAsync())
                    break;
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        await Task.WhenAll(producer, consumer);
    }

    private static void PinToCore(int core)
    {
        if (core < 0 || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        Thread.BeginThreadAffinity();

        var mask = new UIntPtr(1UL << core);

        if (SetThreadAffinityMask(GetCurrentThread(), mask) == UIntPtr.Zero)
        {
            // The core is not allowed by the current process affinity mask — run unpinned instead.
            Thread.EndThreadAffinity();
        }
    }

    private static IntPtr ExpandProcessAffinity()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return IntPtr.Zero;

        try
        {
            var process = Process.GetCurrentProcess();
            var previous = process.ProcessorAffinity;

            // Allow every logical processor so both pinned cores are schedulable.
            var bits = Math.Min(Environment.ProcessorCount, 63);
            process.ProcessorAffinity = new IntPtr((1L << bits) - 1);

            return previous;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static void RestoreProcessAffinity(IntPtr previous)
    {
        if (previous == IntPtr.Zero || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            Process.GetCurrentProcess().ProcessorAffinity = previous;
        }
        catch
        {
            // Best-effort restore.
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);
}
