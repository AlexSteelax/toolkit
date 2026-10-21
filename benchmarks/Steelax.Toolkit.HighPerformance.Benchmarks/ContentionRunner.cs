namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
///     Runs a fixed total number of operations distributed evenly (including the remainder) across
///     dedicated OS threads, so contention benchmarks get an exact per-operation cost regardless
///     of the number of worker threads.
/// </summary>
internal static class ContentionRunner
{
    public static void Run(int threadCount, int totalOperations, Action<int, int> operation)
    {
        var session = Start(threadCount, totalOperations, operation);
        session.Release();
        session.Join();
    }

    public static ContentionSession Start(int threadCount, int totalOperations, Action<int, int> operation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1);

        var threads = new Thread[threadCount];
        var baseOps = totalOperations / threadCount;
        var remainder = totalOperations % threadCount;
        var session = new ContentionSession(threads);

        for (var t = 0; t < threadCount; t++)
        {
            var threadId = t;
            var ops = baseOps + (t < remainder ? 1 : 0);

            threads[t] = new Thread(() =>
            {
                session.WaitForStart();

                for (var k = 0; k < ops; k++)
                    operation(threadId, k);
            })
            {
                IsBackground = true,
                Name = $"contention-{threadId}",
            };

            threads[t].Start();
        }

        return session;
    }
}

/// <summary>
///     A "starting gun": workers block on <see cref="WaitForStart" /> until the caller has created
///     all threads and called <see cref="Release" />, so every worker enters the contended phase
///     simultaneously.
/// </summary>
internal sealed class ContentionSession
{
    private readonly ManualResetEventSlim _gate = new(false);
    private readonly Thread[] _threads;

    public ContentionSession(Thread[] threads) => _threads = threads;

    public void WaitForStart() => _gate.Wait();

    public void Release() => _gate.Set();

    public void Join()
    {
        foreach (var thread in _threads)
            thread.Join();
    }
}