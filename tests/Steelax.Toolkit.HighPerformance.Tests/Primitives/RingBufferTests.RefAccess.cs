using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class RefAccess
    {
        [Fact]
        public void Indexer_Tail_MutatesInPlace()
        {
            var buffer = new RingBuffer<Counter>(4);
            buffer.TryEnqueue(new Counter());
            buffer.TryEnqueue(new Counter());

            ref var tail = ref buffer[buffer.Count - 1];
            tail.Value = 42;

            Assert.True(buffer.TryPeekTail(out var peeked));
            Assert.Equal(42, peeked.Value);
            Assert.Equal(2, buffer.Count);
        }

        [Fact]
        public void Indexer_Head_MutatesInPlace()
        {
            var buffer = new RingBuffer<Counter>(4);
            buffer.TryEnqueue(new Counter());
            buffer.TryEnqueue(new Counter());

            ref var head = ref buffer[0];
            head.Value = 7;

            Assert.True(buffer.TryPeekHead(out var peeked));
            Assert.Equal(7, peeked.Value);
        }

        [Fact]
        public void Indexer_WhenEmpty_ThrowsArgumentOutOfRangeException()
        {
            var buffer = new RingBuffer<int>(2);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[-1]);
        }
    }
}