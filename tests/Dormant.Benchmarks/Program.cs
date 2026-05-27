// BenchmarkDotNet entry point for the comparative ORM suite (feature 008): Dormant vs Dapper, EF Core,
// and Insight.Database over one shared in-memory SQLite database. BenchmarkSwitcher discovers every
// [Benchmark] class in this assembly, so `dotnet run -c Release --project tests/Dormant.Benchmarks` runs
// the lot and `-- --filter '<glob>'` selects a subset. The CI smoke uses `-- --job dry --filter '*'`.
using BenchmarkDotNet.Running;
using Dormant.Benchmarks;

// BenchmarkSwitcher (not BenchmarkRunner) so CLI args work: `--filter '<glob>'` selects a subset and
// `--job dry` runs each benchmark once for the CI smoke. BenchmarkRunner.Run(assembly) ignores args.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig.Create());
return 0;
