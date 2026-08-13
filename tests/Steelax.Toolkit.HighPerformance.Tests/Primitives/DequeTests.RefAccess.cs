using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class DequeTests
{
    public sealed class RefAccess
    {
        [Fact]
        public void Indexer_Last_MutatesInPlace()
        {
            var deque = new Deque<Counter>(4);
            deque.TryAddLast(new Counter());
            deque.TryAddLast(new Counter());

            ref var last = ref deque[deque.Count - 1];
            last.Value = 42;

            Assert.True(deque.TryPeekLast(out var peeked));
            Assert.Equal(42, peeked.Value);
            Assert.Equal(2, deque.Count);
        }

        [Fact]
        public void Indexer_First_MutatesInPlace()
        {
            var deque = new Deque<Counter>(4);
            deque.TryAddLast(new Counter());
            deque.TryAddLast(new Counter());

            ref var first = ref deque[0];
            first.Value = 7;

            Assert.True(deque.TryPeekFirst(out var peeked));
            Assert.Equal(7, peeked.Value);
        }

        [Fact]
        public void Indexer_WhenEmpty_ThrowsArgumentOutOfRangeException()
        {
            var deque = new Deque<int>(2);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = deque[-1]);
        }
    }
}
