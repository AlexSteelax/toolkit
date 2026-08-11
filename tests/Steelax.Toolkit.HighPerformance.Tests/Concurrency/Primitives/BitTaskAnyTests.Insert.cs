using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class BitTaskAnyTests
{
    public sealed class Insert
    {
        [Fact]
        public void Insert_DistinctSlots_ThenFull_ReturnsNoSlot()
        {
            var set = new BitTaskAny(() => { }, 4);

            var s0 = set.Insert(Task.CompletedTask);
            var s1 = set.Insert(Task.CompletedTask);
            var s2 = set.Insert(Task.CompletedTask);
            var s3 = set.Insert(Task.CompletedTask);

            Assert.True(s0 >= 0);
            Assert.True(s1 >= 0);
            Assert.True(s2 >= 0);
            Assert.True(s3 >= 0);
            Assert.Equal(4, set.Count);
            Assert.NotEqual(s0, s1);
            Assert.NotEqual(s0, s3);

            Assert.Equal(BitTaskAny.NoSlot, set.Insert(Task.CompletedTask));
        }

        [Fact]
        public void Capacity32_InsertsAll32Slots()
        {
            var set = new BitTaskAny(() => { });

            for (var i = 0; i < 32; i++)
                Assert.NotEqual(BitTaskAny.NoSlot, set.Insert(Task.CompletedTask));

            Assert.Equal(BitTaskAny.NoSlot, set.Insert(Task.CompletedTask));
            Assert.Equal(32, set.Count);
        }

        [Fact]
        public void CanAdd_ReflectsFreeSpace()
        {
            var set = new BitTaskAny(() => { }, 2);

            Assert.True(set.CanAdd);

            set.Insert(Task.CompletedTask);
            set.Insert(Task.CompletedTask);

            Assert.False(set.CanAdd);
        }

        [Fact]
        public async Task CompletedTask_SignalsSynchronously()
        {
            var signal = new TaskCompletionSource();
            var set = new BitTaskAny(() => signal.TrySetResult());

            set.Insert(Task.CompletedTask);

            await signal.Task;
            Assert.True(set.HasReady);
        }

        [Fact]
        public async Task IncompleteTask_SignalsOnCompletion()
        {
            var signal = new TaskCompletionSource();
            var set = new BitTaskAny(() => signal.TrySetResult());
            var tcs = new TaskCompletionSource();

            set.Insert(tcs.Task);
            Assert.False(set.HasReady);

            tcs.SetResult();
            await signal.Task;

            Assert.True(set.HasReady);
            Assert.Equal(1, set.CountReady);
        }

        [Fact]
        public async Task FaultedTask_BecomesReady_AndRethrowsOnGetResult()
        {
            var signal = new TaskCompletionSource();
            var set = new BitTaskAny(() => signal.TrySetResult());
            var ex = new InvalidOperationException("boom");

            set.Insert(Task.FromException(ex));
            await signal.Task;

            Assert.True(set.TryTake(out _, out var task));
            var thrown = Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.Same(ex, thrown);
        }

        [Fact]
        public async Task CanceledTask_BecomesReady_AndRethrowsOnGetResult()
        {
            var signal = new TaskCompletionSource();
            var set = new BitTaskAny(() => signal.TrySetResult());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            set.Insert(Task.FromCanceled(cts.Token));
            await signal.Task;

            Assert.True(set.HasReady);
            Assert.True(set.TryTake(out _, out var task));
            Assert.True(task.IsCanceled);
            Assert.ThrowsAny<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        }
    }

}