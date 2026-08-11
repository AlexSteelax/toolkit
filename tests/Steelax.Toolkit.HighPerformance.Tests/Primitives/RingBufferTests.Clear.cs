using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class Clear
    {
        [Fact]
        public void Clear_ResetsBuffer()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);

            buffer.Clear();

            Assert.Equal(0, buffer.Count);
            Assert.True(buffer.IsEmpty);
            Assert.False(buffer.TryDequeue(out _));
        }
    }
}