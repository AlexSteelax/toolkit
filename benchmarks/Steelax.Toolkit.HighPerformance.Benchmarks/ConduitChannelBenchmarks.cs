using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Steelax.Toolkit.HighPerformance.Concurrency.Collections;

namespace Steelax.Toolkit.HighPerformance.Benchmarks;

/// <summary>
/// Benchmarks the SPSC "charge/discharge" <see cref="Conduit{T}"/> under the same producer/consumer
/// pattern as the SPSC concurrency tests: a single producer writes <see cref="N"/> values (spinning
/// while the buffer is full), a single consumer drains them through <see cref="Conduit{T}.TryRead"/>.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Config(typeof(Config))]
public partial class ConduitChannelBenchmarks
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
                .WithWarmupCount(5)
                .WithMinIterationCount(10)
                .WithMaxIterationCount(50));
        }
    }
    /// <summary>Buffer capacity (number of buffered values).</summary>
    [Params(4, 16, 64, 256, 512)]
    public int Capacity { get; set; }

    public const int Count = 100_000_000;
}
