using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class SpscQueueTests
{
    public sealed class Overflow
    {
        [Fact(Timeout = 10000)]
        public async Task SequenceIndexes_WrapAround_IntMax_KeepsConsistency()
        {
            // Выставляем монотонные счётчики вплотную к uint.MaxValue, чтобы прогнать
            // переход через границу (uint.MaxValue -> 0) в «непрерывном» потоке:
            // модель «sequence» должна сохранять инвариант через разность,
            // не полагаясь на большой предел счётчика.
            var queue = new SpscQueue<int>(1);
            queue.WriterSeq = uint.MaxValue - 3;
            queue.ReaderSeq = uint.MaxValue - 3;

            const int count = 100;

            var producer = Task.Run(() =>
            {
                var spin = new SpinWait();

                for (var i = 0; i < count; i++)
                {
                    while (!queue.TryWrite(i))
                        spin.SpinOnce();
                }

                queue.TryComplete();
            }, TestContext.Current.CancellationToken);

            var collected = await Task.Run(() => ReadAll(queue), TestContext.Current.CancellationToken);

            await producer.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            Assert.Equal(count, collected.Count);
            Assert.Equal((long)count * (count - 1) / 2, collected.Sum(x => (long)x));

            Assert.True(queue.WriterSeq < 1_000);
            Assert.True(queue.ReaderSeq < 1_000);
        }

        [Fact]
        public void Count_SurvivesSequenceWrapAround()
        {
            // Счётчики вплотную к uint.MaxValue: WriterSeq переполняется (uint.MaxValue -> 0),
            // ReaderSeq ещё нет. Разность в uint (модульная арифметика) должна давать точный Count.
            var queue = new SpscQueue<int>(8);
            queue.WriterSeq = uint.MaxValue - 2;
            queue.ReaderSeq = uint.MaxValue - 2;

            // Пусто до записи: оба счётчика на uint.MaxValue - 2.
            Assert.Equal(uint.MaxValue - 2, queue.WriterSeq);
            Assert.Equal(uint.MaxValue - 2, queue.ReaderSeq);
            Assert.Equal(0, queue.Count);

            // Записываем 3 элемента: WriterSeq переходит через uint.MaxValue (→ 0).
            Assert.True(queue.TryWrite(10));
            Assert.True(queue.TryWrite(20));
            Assert.True(queue.TryWrite(30));

            // WriterSeq уже «обернулся» в 0, ReaderSeq ещё нет (uint.MaxValue - 2).
            Assert.Equal(0u, queue.WriterSeq);
            Assert.Equal(uint.MaxValue - 2, queue.ReaderSeq);
            // Модульная разность uint = 3 → Count корректен, несмотря на wrap-around WriterSeq.
            Assert.Equal(3, queue.Count);

            // Вычитываем по одному, проверяя Count после каждого извлечения.
            Assert.True(queue.TryRead(out var a));
            Assert.Equal(10, a);
            Assert.Equal(2, queue.Count);

            Assert.True(queue.TryRead(out var b));
            Assert.Equal(20, b);
            Assert.Equal(1, queue.Count);
            Assert.Equal(uint.MaxValue, queue.ReaderSeq);   // uint.MaxValue - 2 + 2

            // Вычитываем последний — ReaderSeq оборачивается в 0, Count = 0.
            Assert.True(queue.TryRead(out var c));
            Assert.Equal(30, c);
            Assert.Equal(0u, queue.WriterSeq);
            Assert.Equal(0u, queue.ReaderSeq);
            Assert.Equal(0, queue.Count);
        }
    }
}
