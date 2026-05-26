using BenchmarkDotNet.Attributes;
using Dapper;
using Dormant.Benchmarks.Infrastructure;
using Dormant.Benchmarks.Schema.Bench;
using Insight.Database;
using Microsoft.EntityFrameworkCore;

namespace Dormant.Benchmarks.Benchmarks;

/// <summary>
/// OP-4: set one row's quantity by key, for each library (Dormant = baseline). Each library owns a distinct
/// pre-seeded scratch row (<see cref="SqliteBenchHarness.UpdateKeys"/>), so updates are idempotent and never
/// collide. EF uses the set-based <c>ExecuteUpdate</c> so every library issues a single UPDATE statement.
/// </summary>
public class UpdateBenchmarks : BenchmarkBase
{
    private const string Sql = "UPDATE bench_product SET quantity = @quantity WHERE id = @id";
    private const int NewQuantity = 42;

    [Benchmark(Baseline = true)]
    public async Task Dormant()
    {
        await using var session = await Harness.OpenDormantSessionAsync();
        await session.UpdateProductQuantity(Harness.UpdateKeys[(int)OrmKind.Dormant], NewQuantity);
        await session.CommitAsync();
    }

    [Benchmark]
    public async Task Dapper()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        await SqlMapper.ExecuteAsync(
            connection,
            Sql,
            new { quantity = NewQuantity, id = Harness.UpdateKeys[(int)OrmKind.Dapper] }
        );
    }

    [Benchmark]
    public async Task EfCore()
    {
        var id = Harness.UpdateKeys[(int)OrmKind.EfCore];
        await using var context = Harness.NewEfContext();
        await context
            .Products.Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Quantity, NewQuantity));
    }

    [Benchmark]
    public async Task Insight()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        await connection.ExecuteSqlAsync(
            Sql,
            new Dictionary<string, object>
            {
                ["quantity"] = NewQuantity,
                ["id"] = Harness.UpdateKeys[(int)OrmKind.Insight],
            }
        );
    }
}
