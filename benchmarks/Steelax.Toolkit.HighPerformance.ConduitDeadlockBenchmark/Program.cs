using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks.Sources;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

var failed = 0;

foreach (var cap in new[] { 4, 16, 64, 512 })
{
    for (var r = 0; r < 200; r++)
    {
        var queue = new Conduit<int>(cap, ConduitBehavior.AwaitableReader | ConduitBehavior.AwaitableWriter);
        const int n = 100_000_000;

        using var probe = new ChannelProbe(queue);
        probe.Start();

        var producer = Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < n; i++)
            {
                while (!queue.TryWrite(i))
                    if (!await queue.WaitToWriteAsync())
                        break;
            }
            queue.TryComplete();
        }, TaskCreationOptions.LongRunning).Unwrap();

        var consumer = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (queue.TryRead(out _))
                    continue;
                if (!await queue.WaitToReadAsync())
                    break;
            }
        }, TaskCreationOptions.LongRunning).Unwrap();

        var all = Task.WhenAll(producer, consumer);

        if (!all.Wait(200_000))
        {
            failed++;
            Console.WriteLine($"!!! HANG: cap={cap} round={r} (producer={producer.Status}, consumer={consumer.Status})");
            probe.Dump("HANG");
            return;
        }

        probe.Stop();

        if (r % 5 == 0) Console.WriteLine($"ok: cap={cap} round={r}");
    }
}

Console.WriteLine(failed == 0 ? "ALL PASSED" : $"FAILED: {failed}");

/// <summary>
/// Diagnostic probe: a background thread periodically reads the internal state of the channel and its
/// readiness signals via reflection and prints it, so a hang can be attributed to a stuck side.
/// </summary>
sealed class ChannelProbe : IDisposable
{
    private readonly Conduit<int> _channel;
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitor;

    private static readonly Type Core = typeof(Conduit<int>);
    private static readonly Type Chan = typeof(Conduit<int>);
    private static readonly Type Sig = typeof(CompleteSignal);
    private static readonly Type Mrvs = typeof(ManualResetValueTaskSourceCore<bool>);

    private static readonly FieldInfo WSeq = F(Core, "WriterSeq");
    private static readonly FieldInfo RSeq = F(Core, "ReaderSeq");
    private static readonly FieldInfo Closed = F(Core, "_closed");
    private static readonly FieldInfo Completed = F(Core, "_completed");
    private static readonly FieldInfo Version = F(Core, "_version");
    private static readonly FieldInfo ReaderSignal = F(Chan, "_readerSignal");
    private static readonly FieldInfo WriterSignal = F(Chan, "_writerSignal");
    private static readonly FieldInfo SigState = F(Sig, "_state");
    private static readonly FieldInfo SigDone = F(Sig, "_completed");
    private static readonly FieldInfo SigCore = F(Sig, "_core");
    private static readonly FieldInfo? CoreVersion = FNullable(Mrvs, "_version");
    private static readonly FieldInfo? CoreDone = FNullable(Mrvs, "_completed");
    private static readonly FieldInfo? CoreCont = FNullable(Mrvs, "_continuation");

    private static FieldInfo F(Type t, string name) =>
        t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException($"Field {t.Name}.{name} not found");

    private static FieldInfo? FNullable(Type t, string name) =>
        t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public ChannelProbe(Conduit<int> channel) => _channel = channel;

    public void Start()
    {
        _monitor = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            var lastW = 0u;
            var lastR = 0u;
            var stalled = 0;

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var (w, r) = (WSeq.GetValue(_channel) is uint ww ? ww : 0,
                                  RSeq.GetValue(_channel) is uint rr ? rr : 0);

                    if (w == lastW && r == lastR)
                        stalled++;
                    else
                        stalled = 0;

                    lastW = w;
                    lastR = r;

                    var flag = stalled >= 2 ? " *** STALLED ***" : "";
                    Console.WriteLine($"[mon] {sw.Elapsed.TotalSeconds,6:F1}s{flag} " + Snapshot());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[mon] probe error: {ex.Message}");
                }

                await Task.Delay(1000);
            }
        });
    }

    public void Stop()
    {
        _cts.Cancel();
        _monitor?.Wait(2000);
    }

    public void Dump(string tag) => Console.WriteLine($"[{tag}] " + Snapshot());

    private string Snapshot()
    {
        var w = (uint)WSeq.GetValue(_channel)!;
        var r = (uint)RSeq.GetValue(_channel)!;
        var closed = (bool)Closed.GetValue(_channel)!;
        var completed = (bool)Completed.GetValue(_channel)!;
        var version = (int)Version.GetValue(_channel)!;

        return $"W={w} R={r} cnt={(int)(w - r)} closed={closed} comp={completed} ver={version} | " +
               $"rSig{SignalState(ReaderSignal.GetValue(_channel)!)} | " +
               $"wSig{SignalState(WriterSignal.GetValue(_channel)!)}";
    }

    private string SignalState(object signal)
    {
        var state = (int)SigState.GetValue(signal)!;
        var done = (bool)SigDone.GetValue(signal)!;
        var core = SigCore.GetValue(signal)!;

        var v = CoreVersion?.GetValue(core)?.ToString() ?? "?";
        var c = CoreDone?.GetValue(core)?.ToString() ?? "?";
        var waiting = CoreCont?.GetValue(core) is not null;

        return $"{{st={state} done={done} core{{v={v} c={c} wait={waiting}}}}}";
    }

    public void Dispose() => Stop();
}
