using Xunit;

namespace Steelax.Toolkit.HighPerformance.Exploration;

public class AsyncEnumeratorBehaviorTests
{
    [Fact]
    public async Task MoveNextException()
    {
        var stream = new TestStream();
        await using var enumerator = stream.GetAsyncFaultEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, enumerator.Current);

        var next = enumerator.MoveNextAsync();
        Assert.True(next.IsCompleted);
        Assert.False(next.IsCompletedSuccessfully);
        
        Assert.False(await enumerator.MoveNextAsync());
        Assert.False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task Test2()
    {
        var stream = new TestStream();
        await using var enumerator = stream.GetAsyncNothingEnumerator();
        
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, enumerator.Current);
        
        Assert.False(await enumerator.MoveNextAsync());
        Assert.NotEqual(1, enumerator.Current);
    }

    private class TestStream
    {
        public async IAsyncEnumerator<int> GetAsyncFaultEnumerator()
        {
            yield return 1;
            yield return ThrowException();
            yield return 2;
        }

        public async IAsyncEnumerator<int> GetAsyncNothingEnumerator()
        {
            yield return 1;
        }

        private static int ThrowException()
        {
            // throw new OperationCanceledException();
            throw new InvalidOperationException();
        }
    }
}