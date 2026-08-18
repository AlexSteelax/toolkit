using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Perfolizer.Horology;
using Steelax.Toolkit.HighPerformance.Concurrency;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
/// Benchmarks the SPSC "charge/discharge" <see cref="SpscChannel{T}"/> under the same
/// producer/consumer pattern as the <c>ConcurrentProducerConsumer_SmallLimit_NoLoss</c> test:
/// a single producer writes <see cref="N"/> values (spinning while the buffer is full),
/// a single consumer drains them through <see cref="SpscChannel{T}T}ryRead"/>.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Config(typeof(Config))]
public partial class SpscQueueBenchmarks
{
    /// <summary>
    /// Достоверность замера определяется количеством обработанных элементов (N), поэтому
    /// разогрев не нужен, а число итераций достаточно ограничить одной.
    /// </summary>
    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams);
            AddJob(Job.Default
                .WithWarmupCount(2)
                .WithMinIterationCount(10)
                .WithMaxIterationCount(50));
        }
    }
    /// <summary>Buffer capacity (number of buffered values).</summary>
    [Params(4, 16, 64, 256, 512)]
    public int Capacity { get; set; }
}
