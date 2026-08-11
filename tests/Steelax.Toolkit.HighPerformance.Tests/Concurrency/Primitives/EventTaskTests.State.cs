using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventTaskTests
{
    public sealed class State
    {
        [Fact]
        public void BeforeObserve_StateIsDefault()
        {
            var task = new EventTask<int>();

            Assert.Equal(default(EventTaskState), task.GetState());
        }

        [Fact]
        public void GetResultBeforeObserve_Throws()
        {
            var task = new EventTask<int>();

            Assert.Throws<InvalidOperationException>(() => task.GetResult());
        }

        [Fact]
        public void SyncCompleted_StateIsCompletedSuccessfully()
        {
            var task = new EventTask<int>();

            task.Observe(new ValueTask<int>(42));

            Assert.True(task.GetState().IsCompletedSuccessfully);
            Assert.Equal(42, task.GetResult());
        }

        [Fact]
        public void ReadingMultipleTimes_IsStable()
        {
            var task = new EventTask<int>();

            task.Observe(ValueTask.FromResult(42));

            var first = task.GetState();
            var second = task.GetState();
            var third = task.GetState();

            Assert.True(first.IsCompletedSuccessfully);
            Assert.Equal(first, second);
            Assert.Equal(second, third);
            Assert.Equal(42, task.GetResult());
        }

        [Fact]
        public void NonGeneric_SyncCompleted_StateIsCompletedSuccessfully()
        {
            var task = new EventTask();

            task.Observe(ValueTask.CompletedTask);

            Assert.True(task.GetState().IsCompletedSuccessfully);
        }
    }
}
