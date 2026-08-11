using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class BitTaskAnyTests
{
    public sealed class Signal
    {
        [Fact]
        public void RaisesOncePerBatch_UntilDrained()
        {
            var signalCount = 0;
            var set = new BitTaskAny(() => signalCount++, 2);

            var t1 = new TaskCompletionSource();
            var t2 = new TaskCompletionSource();
            set.Insert(t1.Task);
            set.Insert(t2.Task);

            t1.SetResult();
            t2.SetResult();

            // Edge-triggered: only the 0→1 transition raised the signal.
            Assert.Equal(1, signalCount);
            Assert.Equal(2, set.CountReady);

            Assert.True(set.TryTake(out _, out _));
            Assert.True(set.TryTake(out _, out _));
            Assert.False(set.HasReady);
            Assert.Equal(0, set.Count);

            // A fresh batch after draining raises the signal again.
            var t3 = new TaskCompletionSource();
            set.Insert(t3.Task);

            t3.SetResult();
            Assert.Equal(2, signalCount);
            Assert.True(set.HasReady);
        }
    }
}