using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventTaskTests
{
    public sealed class Faulted
    {
        [Fact]
        public async Task FaultedTask_StateIsFaulted_ExceptionCaptured()
        {
            var ex = new InvalidOperationException("Test error");
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask<int>();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask<int>(Task.FromException<int>(ex)));
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsFaulted);
            Assert.Same(ex, task.Exception);
            Assert.Same(ex, Assert.Throws<InvalidOperationException>(() => task.GetResult()));
        }

        [Fact]
        public async Task NonGeneric_Faulted_StateIsFaulted()
        {
            var ex = new InvalidOperationException("Test error");
            var callbackTcs = new TaskCompletionSource();
            var task = new EventTask();
            task.OnReady += () => callbackTcs.TrySetResult();

            task.Observe(new ValueTask(Task.FromException(ex)));
            await callbackTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(task.GetState().IsFaulted);
            Assert.Same(ex, task.Exception);
        }
    }
}
