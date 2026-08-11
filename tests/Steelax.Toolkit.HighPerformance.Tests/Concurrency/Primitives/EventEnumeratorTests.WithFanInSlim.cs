using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class WithFanInSlim
    {
        [Fact]
        public async Task MoveNext_SignalsSlot_AndFanInWakes()
        {
            var fanIn = new FanInSlim();
            const int slot = 0;
            var source = new[] { 1, 2 }.ToAsyncEnumerable();
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => fanIn.Signal(slot);

            adapter.MoveNext();

            await fanIn.WaitAsync();
            var slots = fanIn.Take();

            Assert.True(slots.IsSet(slot));
            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(1, adapter.GetResult());
        }

        [Fact]
        public async Task AsyncCompletion_SignalsSlot_AndFanInWakes()
        {
            var fanIn = new FanInSlim();
            const int slot = 0;
            var tcs = new TaskCompletionSource<int>();
            var source = AsyncEnumerableFactory.Create([tcs]);
            var adapter = source.GetAsyncEnumerator(TestContext.Current.CancellationToken).AsNonBlocking();
            adapter.OnReady += () => fanIn.Signal(slot);

            adapter.MoveNext();
            Assert.True(adapter.GetState().IsPending);

            // Consumer waits on fan-in while the source is in flight.
            var waitTask = fanIn.WaitAsync();
            tcs.SetResult(7);
            await waitTask.AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var slots = fanIn.Take();
            Assert.True(slots.IsSet(slot));
            Assert.True(adapter.GetState().IsCompletedSuccessfully);
            Assert.Equal(7, adapter.GetResult());
        }
    }
}