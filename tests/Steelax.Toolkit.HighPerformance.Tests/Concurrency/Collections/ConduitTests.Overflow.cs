using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency.Collections;

public static partial class ConduitTests
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
            // true) -> 0) для разрядной модели без большого лимита счётчика;
            // capacity = count снимает блокировку продюсера → детерминированный сбор после завершения.
            const int count = 100;

            var conduit = new Conduit<int>(count);
            conduit.WriterSeq = uint.MaxValue - 3;
            conduit.ReaderSeq = uint.MaxValue - 3;

            var producer = Task.Run(() =>
            {
                var spin = new SpinWait();

                for (var i = 0; i < count; i++)
                {
                    while (!conduit.TryWrite(i))
                        spin.SpinOnce();
                }

                conduit.TryComplete();
            }, TestContext.Current.CancellationToken);

            // Wait for the producer to finish writing and latch completion, then drain deterministically.
            await producer.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            var collected = ReadAll(conduit);

            Assert.Equal(count, collected.Count);
            Assert.Equal((long)count * (count - 1) / 2, collected.Sum(x => (long)x));

            Assert.True(conduit.WriterSeq < 1_000);
            Assert.True(conduit.ReaderSeq < 1_000);
        }

        [Fact]
        public void Count_SurvivesSequenceWrapAround()
        {
            // Счётчики вплотную к uint.MaxValue: WriterSeq переполняется (uint.MaxValue -> 0),
            // ReaderSeq ещё нет. Разность в uint (модульная арифметика) должна давать точный Count.
            var conduit = new Conduit<int>(8);
            conduit.WriterSeq = uint.MaxValue - 2;
            conduit.ReaderSeq = uint.MaxValue - 2;

            // Пусто до записи: оба счётчика на uint.MaxValue - 2.
            Assert.Equal(uint.MaxValue - 2, conduit.WriterSeq);
            Assert.Equal(uint.MaxValue - 2, conduit.ReaderSeq);
            Assert.Equal(0, conduit.Count);

            // Записываем 3 элемента: WriterSeq переходит через uint.MaxValue (→ 0).
            Assert.True(conduit.TryWrite(10));
            Assert.True(conduit.TryWrite(20));
            Assert.True(conduit.TryWrite(30));

            // WriterSeq уже «обернулся» в 0, ReaderSeq ещё нет (uint.MaxValue - 2).
            Assert.Equal(0u, conduit.WriterSeq);
            Assert.Equal(uint.MaxValue - 2, conduit.ReaderSeq);
            // Модульная разность uint = 3 → Count корректен, несмотря на wrap-around WriterSeq.
            Assert.Equal(3, conduit.Count);

            // Вычитываем по одному, проверяя Count после каждого извлечения.
            Assert.True(conduit.TryRead(out var a));
            Assert.Equal(10, a);
            Assert.Equal(2, conduit.Count);

            Assert.True(conduit.TryRead(out var b));
            Assert.Equal(20, b);
            Assert.Equal(1, conduit.Count);
            Assert.Equal(uint.MaxValue, conduit.ReaderSeq);   // uint.MaxValue - 2 + 2

            // Вычитываем последний — ReaderSeq оборачивается в 0, Count = 0.
            Assert.True(conduit.TryRead(out var c));
            Assert.Equal(30, c);
            Assert.Equal(0u, conduit.WriterSeq);
            Assert.Equal(0u, conduit.ReaderSeq);
            Assert.Equal(0, conduit.Count);
        }
    }
}
