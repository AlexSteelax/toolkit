using Steelax.Toolkit.HighPerformance.Concurrency.Primitives;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Primitives;

public static partial class EventEnumeratorTests
{
    public sealed class ResultStateValues
    {
        [Fact]
        public void Factories_ProduceExpectedFlags()
        {
            Assert.True(EventEnumeratorState.Pending().IsPending);
            Assert.True(EventEnumeratorState.CompletedSuccessfully().IsCompletedSuccessfully);
            Assert.True(EventEnumeratorState.Canceled().IsCanceled);
            Assert.True(EventEnumeratorState.Faulted().IsFaulted);
            Assert.True(EventEnumeratorState.EndOfStream().IsEndOfStream);
        }

        [Fact]
        public void Default_IsNotAnyState()
        {
            var state = default(EventEnumeratorState);

            Assert.False(state.IsPending);
            Assert.False(state.IsCompletedSuccessfully);
            Assert.False(state.IsCanceled);
            Assert.False(state.IsFaulted);
            Assert.False(state.IsEndOfStream);
            Assert.False(state);
        }

        [Fact]
        public void BoolConversion_IsReadyOnly()
        {
            Assert.True(EventEnumeratorState.CompletedSuccessfully());
            Assert.False(EventEnumeratorState.Pending());
            Assert.False(EventEnumeratorState.Canceled());
            Assert.False(EventEnumeratorState.Faulted());
            Assert.False(EventEnumeratorState.EndOfStream());
        }

        [Fact]
        public void States_AreDistinct()
        {
            EventEnumeratorState[] states =
            [
                EventEnumeratorState.Pending(),
                EventEnumeratorState.CompletedSuccessfully(),
                EventEnumeratorState.Canceled(),
                EventEnumeratorState.Faulted(),
                EventEnumeratorState.EndOfStream()
            ];

            Assert.Equal(5, states.Distinct().Count());
        }

        [Fact]
        public void EqualStates_AreEqual()
        {
            Assert.Equal(EventEnumeratorState.CompletedSuccessfully(), EventEnumeratorState.CompletedSuccessfully());
            Assert.NotEqual(EventEnumeratorState.CompletedSuccessfully(), EventEnumeratorState.Pending());
        }
    }
}