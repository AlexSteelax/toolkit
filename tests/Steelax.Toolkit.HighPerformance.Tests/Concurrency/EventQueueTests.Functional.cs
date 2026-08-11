using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class EventQueueTests
{
    public sealed class Functional
    {
        [Fact]
        public async Task ReadInOrder_YieldsWrittenItems()
        {
            var queue = new EventQueue<int>(8);

            for (var i = 0; i < 5; i++)
                Assert.True(queue.TryWrite(i));

            // Читаем ровно 5 элементов — поток не завершается (Complete не вызывался),
            // поэтому нельзя использовать ReadAllAsync (он ждёт Completed).
            var collected = new List<int>();
            while (collected.Count < 5)
            {
                await queue.WaitToReadAsync();
                if (queue.TryRead(out var value, out _))
                    collected.Add(value);
            }

            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, collected);
        }

        [Fact]
        public void FullBuffer_RejectsWrites()
        {
            var queue = new EventQueue<int>(2);

            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));
            Assert.False(queue.TryWrite(3));
        }

        [Fact]
        public async Task ConsumerWaitsUntilData_ThenReads()
        {
            var queue = new EventQueue<int>(4);

            var wait = queue.WaitToReadAsync();
            await Task.Delay(50, TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted); // no data yet — still pending

            Assert.True(queue.TryWrite(42));

            await wait;

            Assert.True(queue.TryRead(out var value, out _));
            Assert.Equal(42, value);
        }

        [Fact]
        public void Complete_EndOfStream()
        {
            var queue = new EventQueue<int>(4);
            queue.Complete();

            _ = queue.TryRead(out _, out var completed);
            
            Assert.True(completed);
        }

        [Fact]
        public async Task CompleteAfterData_DrainsThenEnds()
        {
            var queue = new EventQueue<int>(4);
            Assert.True(queue.TryWrite(1));
            Assert.True(queue.TryWrite(2));
            queue.Complete();

            var collected = await ReadAllAsync(queue);

            Assert.Equal(new[] { 1, 2 }, collected);
        }

        [Fact]
        public void Fault_ThrowsOnTryRead()
        {
            var queue = new EventQueue<int>(4);
            var ex = new InvalidOperationException("boom");
            queue.Complete(ex);

            var thrown = Assert.Throws<InvalidOperationException>(() => queue.TryRead(out _, out _));
            Assert.Same(ex, thrown);
        }

        [Fact]
        public void TryWriteAfterComplete_ReturnsFalse()
        {
            var queue = new EventQueue<int>(4);
            queue.Complete();

            Assert.False(queue.TryWrite(1));
        }

        [Fact]
        public async Task OnWriteReady_RaisesWhenSlotFreed()
        {
            var queue = new EventQueue<int>(1);
            Assert.True(queue.TryWrite(1));

            var raised = 0;
            queue.OnWriteReady += () => raised++;

            Assert.True(queue.TryRead(out _, out _));

            Assert.True(raised > 0);
        }

        [Fact(Timeout = 1000)]
        public async Task AwaitingStaleWait_Throws()
        {
            // Аналог enumerator-контрактных тестов оригинала: ожидание на устаревшем токене
            // IValueTaskSource должно бросать InvalidOperationException.
            var queue = new EventQueue<int>(4);

            // Первый WaitToReadAsync регистрирует ожидание на core (версия V0).
            var stale = queue.WaitToReadAsync();
            Assert.False(stale.IsCompleted); // данных ещё нет

            // Сигнал заряжается; первый ожидатель завершается, значение читается.
            Assert.True(queue.TryWrite(1));
            await stale;
            Assert.True(queue.TryRead(out var value, out _));
            Assert.Equal(1, value);

            // Опустошаем буфер: неудачный TryRead снимает взведённый сигнал (2 → 0).
            Assert.False(queue.TryRead(out _, out _));

            // Следующий WaitToReadAsync идёт в case 0 → Reset инкрементит версию core (V0 → V1).
            _ = queue.WaitToReadAsync();

            // Повторное ожидание старого ValueTask — токен V0 устарел → контрактное исключение.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await stale);
        }
    }
}
