using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks.Sources;
using JetBrains.Annotations;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;
using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

public static partial class ConduitTests
{
    internal class ChannelProbe<T> : SignalProbe
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_writerSignal")]
        private static extern ref CompleteSignal? GetWriterSignal(Conduit<T> instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_readerSignal")]
        private static extern ref CompleteSignal? GetReaderSignal(Conduit<T> instance);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "WriterSeq")]
        private static extern ref uint GetWriterSeq(Conduit<T> instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "ReaderSeq")]
        private static extern ref uint GetReaderSeq(Conduit<T> instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_closed")]
        private static extern ref bool GetClosed(Conduit<T> instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_completed")]
        private static extern ref bool GetCompleted(Conduit<T> instance);

        public static string Dump(Conduit<T> conduit)
        {
            var sb = new StringBuilder();

            WriterConduitState(sb, conduit);

            var reader = GetReaderSignal(conduit);
            sb.Append(" | ").Append("rSig>");
            WriterCompleteSignalState(sb, reader);
            
            var writer = GetWriterSignal(conduit);
            sb.Append(" | ").Append("wSig>");
            WriterCompleteSignalState(sb, writer);

            return sb.ToString();
        }
        
        private static void WriterConduitState(StringBuilder sb, Conduit<T> instance)
        {
            sb.Append("w=").Append(Volatile.Read(ref GetWriterSeq(instance))).Append(' ');
            sb.Append("r=").Append(Volatile.Read(ref GetReaderSeq(instance))).Append(' ');
            sb.Append("cnt=").Append(instance.Count).Append(' ');
            sb.Append("cls=").Append(Volatile.Read(ref GetClosed(instance))).Append(' ');
            sb.Append("cmp=").Append(Volatile.Read(ref GetCompleted(instance))).Append(' ');
            sb.Append("ver").Append(instance.Version);
        }
    }
    
    internal class SignalProbe
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_handshake")]
        private static extern ref int GetSignalHandshake(CompleteSignal instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_completed")]
        private static extern ref bool GetSignalDone(CompleteSignal instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_core")]
        private static extern ref ManualResetValueTaskSourceCore<bool> GetCore(CompleteSignal instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_continuation")]
        private static extern ref Action<object>? GetCoreContinuation(ref ManualResetValueTaskSourceCore<bool> instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_completed")]
        private static extern ref bool GetCoreCompleted(ref ManualResetValueTaskSourceCore<bool> instance);
        
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
        private static extern ref short GetCoreVersion(ref ManualResetValueTaskSourceCore<bool> instance);
        
        public static string Dump(CompleteSignal cs)
        {
            var sb = new StringBuilder();
            
            WriterCompleteSignalState(sb, cs);

            return sb.ToString();
        }

        protected static void WriterCompleteSignalState(StringBuilder sb, CompleteSignal? instance)
        {
            if (instance is null)
                return;
            
            sb.Append("hs=").Append(Volatile.Read(ref GetSignalHandshake(instance))).Append(' ');
            sb.Append("done=").Append(Volatile.Read(ref GetSignalDone(instance))).Append(' ');
            
            ref var wCore = ref GetCore(instance);

            sb.Append("core>")
                .Append("v=").Append(Volatile.Read(ref GetCoreVersion(ref wCore))).Append(' ')
                .Append("c=").Append(Volatile.Read(ref GetCoreCompleted(ref wCore))).Append(' ')
                .Append("wait=").Append(Volatile.Read(ref GetCoreContinuation(ref wCore)) is not null);
        }
    }
    
    

    [PublicAPI]
    public sealed class ProbeTracker(Func<string> handler)
    {
        private readonly List<(string Content, ulong Hash)> _output = [];
        private readonly CancellationTokenSource _cts = new();

        private Task? _task;

        public bool IsFault
        {
            get => Volatile.Read(ref field);
            private set => Volatile.Write(ref field, value);
        }

        public bool IsRunning
        {
            get => Volatile.Read(ref field);
            private set => Volatile.Write(ref field, value);
        }

        private void WriteDump()
        {
            var dump = handler.Invoke();
            var hash = XxHash64.HashToUInt64(MemoryMarshal.Cast<char, byte>(dump));

            if (_output.Count > 0 && _output.Last().Hash == hash)
                return;
            
            _output.Add((dump, hash));
        }

        public void Run()
        {
            try
            {
                IsRunning = true;

                lock (_output)
                {
                    _task ??= Task.Factory.StartNew(() =>
                    {
                        while (!_cts.IsCancellationRequested)
                        {
                            WriteDump();
                        }
                    }, TaskCreationOptions.LongRunning);
                }
            }
            catch
            {
                IsFault = true;
            }
            finally
            {
                IsRunning = false;
            }
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public void ToOutput(ITestOutputHelper output)
        {
            if (IsRunning)
                throw new InvalidOperationException();
            
            foreach (var item in _output)
                output.WriteLine(item.Content);
        }
    }
}