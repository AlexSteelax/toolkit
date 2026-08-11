using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class BitTaskAnyTests
{
    public sealed class Take
    {
        [Fact]
        public void WhenNoneReady_ReturnsFalse()
        {
            var set = new BitTaskAny(() => { });

            Assert.False(set.TryTake(out var index, out var task));
            Assert.Equal(BitTaskAny.NoSlot, index);
            Assert.Null(task);
        }

        [Fact]
        public async Task CompletedTask_IsTaken_AndSlotFreed()
        {
            var signal = new TaskCompletionSource();
            var set = new BitTaskAny(() => signal.TrySetResult(), 1);
            var tcs = new TaskCompletionSource();

            set.Insert(tcs.Task);
            tcs.SetResult();
            await signal.Task;

            Assert.True(set.TryTake(out _, out var task));
            Assert.True(task.IsCompletedSuccessfully);

            Assert.Equal(0, set.Count);
            Assert.Equal(0, set.CountReady);
            Assert.False(set.HasReady);
            Assert.True(set.CanAdd);
            Assert.False(set.TryTake(out _, out _));
        }

        [Fact]
        public void SlotIsReusableAfterTake()
        {
            var set = new BitTaskAny(() => { }, 1);

            var first = set.Insert(Task.CompletedTask);
            Assert.True(set.TryTake(out var taken, out _));
            Assert.Equal(first, taken);

            var second = set.Insert(Task.CompletedTask);
            Assert.Equal(first, second); // single slot is reused
        }

        [Fact]
        public void MultipleTasks_AllCanBeTaken_WithIndexRoundTrip()
        {
            var set = new BitTaskAny(() => { }, 3);

            var inserted = new List<int>();
            for (var i = 0; i < 3; i++)
                inserted.Add(set.Insert(Task.CompletedTask));

            var taken = new List<int>();
            while (set.TryTake(out var index, out _))
                taken.Add(index);

            Assert.Equal(3, taken.Count);
            Assert.Equal(3, inserted.Distinct().Count());
            foreach (var index in inserted)
                Assert.Contains(index, taken);
        }
    }

}