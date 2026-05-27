using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.EntityFrameworkCore;
using DormantProduct = Dormant.Benchmarks.Schema.Bench.Product;
using PocoProduct = Dormant.Benchmarks.Model.Product;

namespace Dormant.Benchmarks.Benchmarks;

/// <summary>OP-1: fetch one product by primary key, materialized, for each library (Dormant = baseline).</summary>
public class ReadByKeyBenchmarks : BenchmarkBase
{
    private const string Sql =
        "SELECT id, name, category, price, quantity FROM bench_product WHERE id = @id";

    [Benchmark(Baseline = true)]
    public async Task<DormantProduct?> Dormant()
    {
        await using var session = await Harness.OpenDormantSessionAsync();
        return await session.GetAsync<DormantProduct>(Harness.ReadKey);
    }

    [Benchmark]
    public async Task<PocoProduct?> Dapper()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var result = await connection.QueryFirstOrDefaultAsync<PocoProduct>(
            Sql,
            new { id = Harness.ReadKey },
            transaction
        );

        await transaction.CommitAsync();
        return result;
    }

    [Benchmark]
    public async Task<PocoProduct?> EfCore()
    {
        var id = Harness.ReadKey;
        await using var context = Harness.NewEfContext();
        return await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }
}
