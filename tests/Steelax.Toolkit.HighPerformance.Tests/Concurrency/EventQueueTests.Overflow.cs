using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Tests.Concurrency;

public static partial class EventQueueTests
{
    public sealed class Overflow
    {
        [Fact(Timeout = 10000)]
        public async Task SequenceIndexes_WrapAround_IntMax_KeepsConsistency()
        {
            // Выставляем монотонные счётчики вплотную к uint.MaxValue, чтобы прогнать
            // переход через границу (uint.MaxValue -> 0) в «непрерывном» потоке:
            // модель «sequence + single event» должна сохранять инвариант через разность,
            // не полагаясь на большой предел счётчика.
            var queue = new EventQueue<int>(1);
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

                queue.Complete();
            }, TestContext.Current.CancellationToken);

            var collected = await ReadAllAsync(queue);

            await producer.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

            Assert.Equal(count, collected.Count);
            Assert.Equal((long)count * (count - 1) / 2, collected.Sum(x => (long)x));

            Assert.True(queue.WriterSeq < 1_000);
            Assert.True(queue.ReaderSeq < 1_000);
        }
    }
}
