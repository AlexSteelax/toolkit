using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class BitTaskAnyTests
{
    public sealed class Constructor
    {
        [Fact]
        public void NullSignal_ThrowsArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => new BitTaskAny(null!));

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(33)]
        public void InvalidCapacity_ThrowsArgumentOutOfRangeException(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new BitTaskAny(() => { }, capacity));

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(32)]
        public void ValidCapacity_InitializesState(int capacity)
        {
            var set = new BitTaskAny(() => { }, capacity);

            Assert.Equal(capacity, set.Capacity);
            Assert.Equal(0, set.Count);
            Assert.Equal(0, set.CountReady);
            Assert.False(set.HasReady);
            Assert.True(set.CanAdd);
        }
    }
}