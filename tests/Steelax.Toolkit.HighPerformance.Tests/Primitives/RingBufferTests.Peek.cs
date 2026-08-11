using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class Peek
    {
        [Fact]
        public void PeekHead_ReturnsHead_WithoutRemoving()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);

            Assert.True(buffer.TryPeekHead(out var head));
            Assert.Equal(1, head);
            Assert.Equal(2, buffer.Count);
        }

        [Fact]
        public void PeekTail_ReturnsLast_WithoutRemoving()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);

            Assert.True(buffer.TryPeekTail(out var tail));
            Assert.Equal(2, tail);
            Assert.Equal(2, buffer.Count);
        }

        [Fact]
        public void Peek_WhenEmpty_ReturnsFalse()
        {
            var buffer = new RingBuffer<int>(2);

            Assert.False(buffer.TryPeekHead(out _));
            Assert.False(buffer.TryPeekTail(out _));
        }
    }
}