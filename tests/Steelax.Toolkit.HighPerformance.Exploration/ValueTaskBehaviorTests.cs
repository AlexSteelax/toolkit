using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Sources;
using JetBrains.Annotations;
using Xunit;

namespace Steelax.Toolkit.HighPerformance.Exploration;

public class ValueTaskBehaviorTests
{
    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void ManualSource_Consumption()
    {
        const int value = 42;
        var source = new ManualSource();
        var vt = source.AwaitAsync();
    
        var continuationExecuted = false;

        var awaiter = vt.GetAwaiter();
        awaiter.OnCompleted(() => 
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
        });

        // 2. Имитируем завершение операции
        source.SetResult(value);
        
        Assert.True(Volatile.Read(ref continuationExecuted));
        
        // 4. ПЫТАЕМСЯ ОБРАТИТЬСЯ К VT ПОСЛЕ OnCompleted
        Assert.True(vt.IsCompleted);
        // Первый раз получили результат, ожидаем что все ок
        Assert.Equal(value, vt.Result);
        
        // Второй раз получаем рультат, ожидаем что будет исключение, т.к. ValueTask не поддерживает повторное получение результата
        Assert.ThrowsAny<InvalidOperationException>(() => vt.Result);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void ManualSource_OnCompleted_AlreadyCompleted()
    {
        const int value = 42;
        var source = new ManualSource();
        var vt = source.AwaitAsync();
        
        source.SetResult(value);
        
        var continuationExecuted = false;
        var mre = new ManualResetEventSlim();
        
        var awaiter = vt.GetAwaiter();
        awaiter.OnCompleted(() => 
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
            mre.Set();
        });
        
        Assert.False(continuationExecuted);

        Assert.True(vt.IsCompleted);
        Assert.Equal(value, vt.Result);

        mre.Wait(TestContext.Current.CancellationToken);
        Assert.True(Volatile.Read(ref continuationExecuted));
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void ManualSource_OnCompleted_ExceptionThrow()
    {
        var ex = new Exception("Test");
        var source = new ManualSource();
        var vt = source.AwaitAsync();
        
        source.SetException(ex);
        
        var continuationExecuted = false;
        
        var awaiter = vt.GetAwaiter();
        awaiter.OnCompleted(() => 
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
        });
        
        Assert.False(Volatile.Read(ref continuationExecuted));

        Assert.True(vt.IsCompleted);
        Assert.True(vt.IsFaulted);
        Assert.Equal(ex, vt.AsTask().Exception?.InnerException);
    }

    [Fact]
    public void ValueTask_DefaultIsSuccess()
    {
        ValueTask<bool> task = default!;
        
        Assert.True(task.IsCompleted);
        Assert.True(task.IsCompletedSuccessfully);
    }
    
    public class ManualSource : IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _core = new() { RunContinuationsAsynchronously = false };

        [PublicAPI]
        public ValueTask<int> AwaitAsync() => new(this, _core.Version);

        [PublicAPI]
        public void SetResult(int result) => _core.SetResult(result);
        
        [PublicAPI]
        public void SetException(Exception ex) => _core.SetException(ex);

        int IValueTaskSource<int>.GetResult(short token)
        {
            try
            {
                return _core.GetResult(token);
            }
            finally
            {
                _core.Reset();
            }
        }

        ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token)
        {
            return _core.GetStatus(token);
        }

        void IValueTaskSource<int>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _core.OnCompleted(continuation, state, token, flags);
        }
    }
}