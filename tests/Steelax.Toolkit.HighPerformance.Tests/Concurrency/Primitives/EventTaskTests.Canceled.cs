using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventTaskTests
{
    public sealed class Canceled
    {
        [Fact]
        public async Task CanceledTask_StateIsCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask<int>();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask<int>(Task.FromCanceled<int>(cts.Token)));
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsCanceled);
            Assert.Null(task.Exception);
            Assert.Throws<OperationCanceledException>(() => task.GetResult());
        }

        [Fact]
        public async Task NonGeneric_Canceled_StateIsCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask(Task.FromCanceled(cts.Token)));
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsCanceled);
            Assert.Null(task.Exception);
        }
    }
}
