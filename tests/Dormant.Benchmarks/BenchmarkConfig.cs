using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace Dormant.Benchmarks;

/// <summary>
/// Shared BenchmarkDotNet configuration for the comparative suite (feature 008): the default config plus
/// <see cref="MemoryDiagnoser"/> (allocated bytes/op — FR-004) and a rank column so the relative standing
/// per operation is readable at a glance (SC-004). The Dormant method in each group is the baseline
/// (<c>[Benchmark(Baseline = true)]</c>), so a ratio column is added automatically.
/// </summary>
/// <remarks>
/// The CI smoke run uses BenchmarkDotNet's built-in <c>--job dry</c> (each benchmark once) — no custom job
/// is needed here; passing it on the command line overrides the default job.
/// </remarks>
public static class BenchmarkConfig
{
    public static IConfig Create() =>
        DefaultConfig.Instance.AddDiagnoser(MemoryDiagnoser.Default).AddColumn(RankColumn.Arabic);
}
