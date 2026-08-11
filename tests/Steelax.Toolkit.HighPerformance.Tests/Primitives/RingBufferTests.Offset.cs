using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class Offset
    {
        [Fact]
        public void TryGetAt_ReturnsElementAtOffset()
        {
            var buffer = new RingBuffer<int>(4);
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);
            buffer.TryEnqueue(3);

            Assert.True(buffer.TryGetAt(0, out var head));
            Assert.True(buffer.TryGetAt(1, out var mid));
            Assert.True(buffer.TryGetAt(2, out var tail));

            Assert.Equal(1, head);
            Assert.Equal(2, mid);
            Assert.Equal(3, tail);
        }

        [Fact]
        public void TryGetAt_OutOfRange_ReturnsFalse()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.TryEnqueue(1);

            Assert.False(buffer.TryGetAt(1, out _));
            Assert.False(buffer.TryGetAt(-1, out _));
        }

        [Fact]
        public void Indexer_ReturnsRef_ToOffsetElement()
        {
            var buffer = new RingBuffer<Counter>(4);
            buffer.TryEnqueue(new Counter());
            buffer.TryEnqueue(new Counter());
            buffer.TryEnqueue(new Counter());

            ref var mid = ref buffer[1];
            mid.Value = 42;

            Assert.True(buffer.TryGetAt(1, out var peeked));
            Assert.Equal(42, peeked.Value);
        }

        [Fact]
        public void Indexer_OutOfRange_ThrowsArgumentOutOfRangeException()
        {
            var buffer = new RingBuffer<int>(2);
            buffer.TryEnqueue(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[-1]);
        }
    }
}