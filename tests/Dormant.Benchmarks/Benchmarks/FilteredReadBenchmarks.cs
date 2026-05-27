using BenchmarkDotNet.Attributes;
using Dapper;
using Dormant.Benchmarks.Schema.Bench;
using Microsoft.EntityFrameworkCore;
using DormantProduct = Dormant.Benchmarks.Schema.Bench.Product;
using PocoProduct = Dormant.Benchmarks.Model.Product;

namespace Dormant.Benchmarks.Benchmarks;

/// <summary>OP-2: fetch and fully materialize all products in a category, for each library (Dormant = baseline).</summary>
public class FilteredReadBenchmarks : BenchmarkBase
{
    private const string Sql =
        "SELECT id, name, category, price, quantity FROM bench_product WHERE category = @category";

    [Benchmark(Baseline = true)]
    public async Task<List<DormantProduct>> Dormant()
    {
        await using var session = await Harness.OpenDormantSessionAsync();
        var list = new List<DormantProduct>();
        await foreach (var product in session.ProductsByCategory(Harness.ReadCategory))
        {
            list.Add(product);
        }

        return list;
    }

    [Benchmark]
    public async Task<List<PocoProduct>> Dapper()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Dapper's QueryAsync via the static form so it doesn't collide with Insight's QueryAsync extension.
        var rows = await SqlMapper.QueryAsync<PocoProduct>(
            connection,
            Sql,
            new { category = Harness.ReadCategory },
            transaction
        );

        await transaction.CommitAsync();
        return rows.AsList();
    }

    [Benchmark]
    public async Task<List<PocoProduct>> EfCore()
    {
        var category = Harness.ReadCategory;
        await using var context = Harness.NewEfContext();
        return await context
            .Products.AsNoTracking()
            .Where(p => p.Category == category)
            .ToListAsync();
    }
}
