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
        awaiter.UnsafeOnCompleted(() => 
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
        
        var awaiter = vt.GetAwaiter();
        awaiter.UnsafeOnCompleted(() =>
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
        });
        
        Assert.False(continuationExecuted);

        Assert.True(vt.IsCompleted);
        Assert.Equal(value, vt.Result);

        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref continuationExecuted), TimeSpan.FromSeconds(2)),
            "Continuation never executed");
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
        awaiter.UnsafeOnCompleted(() => 
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
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void TaskBacked_OnCompleted_AlreadyCompleted()
    {
        const int value = 42;
        var vt = new ValueTask<int>(Task.FromResult(value));
        
        var continuationExecuted = false;
        
        var awaiter = vt.GetAwaiter();
        awaiter.UnsafeOnCompleted(() =>
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
        });
        
        // Task-бэкенд: даже если задача УЖЕ завершена, UnsafeOnCompleted НЕ выполняет
        // континуацию инлайново — TaskAwaiter всегда ставит её в очередь (асинхронно).
        Assert.False(Volatile.Read(ref continuationExecuted));

        Assert.True(vt.IsCompleted);
        Assert.Equal(value, vt.Result);

        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref continuationExecuted), TimeSpan.FromSeconds(2)),
            "Continuation never executed");
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void TaskBacked_OnCompleted_AlreadyFaulted()
    {
        var ex = new Exception("Test");
        var vt = new ValueTask<int>(Task.FromException<int>(ex));
        
        var continuationExecuted = false;
        
        var awaiter = vt.GetAwaiter();
        awaiter.UnsafeOnCompleted(() =>
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
        });
        
        Assert.False(Volatile.Read(ref continuationExecuted));

        Assert.True(vt.IsCompleted);
        Assert.True(vt.IsFaulted);
        Assert.Equal(ex, vt.AsTask().Exception?.InnerException);

        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref continuationExecuted), TimeSpan.FromSeconds(2)),
            "Continuation never executed");
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void Default_OnCompleted_AlreadyCompleted()
    {
        // default ValueTask: нет ни Task, ни IValueTaskSource — IsCompleted == true,
        // поэтому "прятать" континуацию некуда.
        ValueTask<int> vt = default;
        
        var continuationExecuted = false;
        
        var awaiter = vt.GetAwaiter();
        awaiter.UnsafeOnCompleted(() =>
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref continuationExecuted, true);
        });
        
        // НЕ инлайново, вопреки ожиданию: даже у default ValueTask континуация
        // ставится в очередь, а не вызывается прямо в UnsafeOnCompleted.
        Assert.False(Volatile.Read(ref continuationExecuted));

        // Ждём, выполнится ли континуация асинхронно.
        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref continuationExecuted), TimeSpan.FromSeconds(2)),
            "Continuation never executed for default ValueTask");
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method")]
    public void ManualSource_OnCompleted_AlreadyCompleted_Twice()
    {
        const int value = 42;
        var source = new ManualSource();
        var vt = source.AwaitAsync();

        source.SetResult(value);

        var first = false;
        var second = false;

        var awaiter = vt.GetAwaiter();
        awaiter.UnsafeOnCompleted(() =>
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref first, true);
        });

        // Вторая регистрация с тем же токеном на уже завершённом core (GetResult ещё не вызывался,
        // Reset не происходил): проверяем, перезаписывает ли она первую континуацию.
        awaiter.UnsafeOnCompleted(() =>
        {
            // ReSharper disable once AccessToModifiedClosure
            Volatile.Write(ref second, true);
        });

        Assert.False(Volatile.Read(ref first));
        Assert.False(Volatile.Read(ref second));

        Assert.Equal(value, vt.Result);

        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref second), TimeSpan.FromSeconds(2)),
            "Continuation never executed");
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