using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventTaskTests
{
    public sealed class Pending
    {
        [Fact]
        public void PendingTask_StateIsPending()
        {
            var tcs = new TaskCompletionSource<int>();
            var task = new EventTask<int>();

            task.Observe(new ValueTask<int>(tcs.Task));

            Assert.True(task.GetState().IsPending);
            Assert.Throws<InvalidOperationException>(() => task.GetResult());
        }

        [Fact]
        public async Task AfterCompletion_StateIsReady_AndOnReadyInvoked()
        {
            var tcs = new TaskCompletionSource<int>();
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask<int>();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask<int>(tcs.Task));
            Assert.True(task.GetState().IsPending);

            tcs.SetResult(42);
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsCompletedSuccessfully);
            Assert.Equal(42, task.GetResult());
        }

        [Fact]
        public void ObserveWhileInFlight_Throws()
        {
            var tcs = new TaskCompletionSource<int>();
            var task = new EventTask<int>();

            task.Observe(new ValueTask<int>(tcs.Task));

            Assert.Throws<InvalidOperationException>(() => task.Observe(new ValueTask<int>(42)));
        }

        [Fact]
        public void NonGeneric_PendingTask_StateIsPending()
        {
            var tcs = new TaskCompletionSource();
            var task = new EventTask();

            task.Observe(new ValueTask(tcs.Task));

            Assert.True(task.GetState().IsPending);
        }

        [Fact]
        public async Task NonGeneric_AfterCompletion_StateIsCompletedSuccessfully()
        {
            var tcs = new TaskCompletionSource();
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask(tcs.Task));

            tcs.SetResult();
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsCompletedSuccessfully);
        }
    }
}
