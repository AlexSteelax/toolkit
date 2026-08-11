using Steelax.Toolkit.HighPerformance.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Primitives;

public static partial class RingBufferTests
{
    public sealed class NonPowerOfTwoCapacity
    {
        [Fact]
        public void Capacity5_Works_WithWrap()
        {
            var buffer = new RingBuffer<int>(5);

            for (var i = 0; i < 5; i++)
                Assert.True(buffer.TryEnqueue(i));
            Assert.True(buffer.IsFull);

            for (var i = 0; i < 5; i++)
            {
                Assert.True(buffer.TryDequeue(out var item));
                Assert.Equal(i, item);
            }

            Assert.True(buffer.IsEmpty);
        }
    }
}