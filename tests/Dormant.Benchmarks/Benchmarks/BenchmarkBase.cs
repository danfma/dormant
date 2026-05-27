using BenchmarkDotNet.Attributes;
using Dormant.Benchmarks.Infrastructure;

namespace Dormant.Benchmarks.Benchmarks;

/// <summary>
/// Shared lifecycle for every operation benchmark: build one seeded <see cref="SqliteBenchHarness"/> in
/// <c>[GlobalSetup]</c> and dispose it in <c>[GlobalCleanup]</c>. Adding a new operation is therefore one
/// new class that derives from this and adds a <c>[Benchmark]</c> method per library, Dormant the baseline
/// (FR-011). Override <see cref="GlobalSetup"/> (calling <c>base</c>) only if the operation needs extra
/// preparation, as the delete benchmark does for its key pool.
/// </summary>
public abstract class BenchmarkBase
{
    protected SqliteBenchHarness Harness { get; private set; } = null!;

    [GlobalSetup]
    public virtual async Task GlobalSetup() => Harness = await SqliteBenchHarness.CreateAsync();

    [GlobalCleanup]
    public async Task GlobalCleanup() => await Harness.DisposeAsync();
}
