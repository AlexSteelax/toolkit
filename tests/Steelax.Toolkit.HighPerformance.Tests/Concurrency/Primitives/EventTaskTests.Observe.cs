using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventTaskTests
{
    public sealed class Observe
    {
        [Fact]
        public async Task SyncCompleted_InvokesOnReady()
        {
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask<int>();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask<int>(42));

            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsCompletedSuccessfully);
            Assert.Equal(42, task.GetResult());
        }

        [Fact]
        public async Task NonGeneric_SyncCompleted_InvokesOnReady()
        {
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(ValueTask.CompletedTask);

            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsCompletedSuccessfully);
        }
    }
}
