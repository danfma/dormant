using BenchmarkDotNet.Attributes;
using Dapper;
using Dormant.Benchmarks.Schema.Bench;
using Insight.Database;
using PocoProduct = Dormant.Benchmarks.Model.Product;

namespace Dormant.Benchmarks.Benchmarks;

/// <summary>
/// OP-3: insert one row with a fresh key per invocation, for each library (Dormant = baseline). No cleanup —
/// the table grows identically for every library, so the relative comparison stays fair. Note Dormant's
/// generated insert also materializes the row via RETURNING (its idiom); the others issue a bare INSERT.
/// </summary>
public class InsertBenchmarks : BenchmarkBase
{
    private const string Sql =
        "INSERT INTO bench_product (id, name, category, price, quantity) VALUES (@id, @name, @category, @price, @quantity)";

    [Benchmark(Baseline = true)]
    public async Task Dormant()
    {
        await using var session = await Harness.OpenDormantSessionAsync();
        await session.CreateProduct(Guid.NewGuid(), "ins", "ins", 1.23m, 1);
        await session.CommitAsync();
    }

    [Benchmark]
    public async Task Dapper()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        await SqlMapper.ExecuteAsync(
            connection,
            Sql,
            new
            {
                id = Guid.NewGuid(),
                name = "ins",
                category = "ins",
                price = 1.23m,
                quantity = 1,
            }
        );
    }

    [Benchmark]
    public async Task EfCore()
    {
        await using var context = Harness.NewEfContext();
        context.Products.Add(
            new PocoProduct
            {
                Id = Guid.NewGuid(),
                Name = "ins",
                Category = "ins",
                Price = 1.23m,
                Quantity = 1,
            }
        );
        await context.SaveChangesAsync();
    }

    [Benchmark]
    public async Task Insight()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        // Dictionary params: Insight reuses stale anonymous-type parameter values with Microsoft.Data.Sqlite.
        await connection.ExecuteSqlAsync(
            Sql,
            new Dictionary<string, object>
            {
                ["id"] = Guid.NewGuid(),
                ["name"] = "ins",
                ["category"] = "ins",
                ["price"] = 1.23m,
                ["quantity"] = 1,
            }
        );
    }
}
