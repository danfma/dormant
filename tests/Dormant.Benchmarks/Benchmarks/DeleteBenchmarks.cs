using BenchmarkDotNet.Attributes;
using Dapper;
using Dormant.Benchmarks.Schema.Bench;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Dormant.Benchmarks.Benchmarks;

/// <summary>
/// OP-5: delete one row by key, for each library (Dormant = baseline). A deleted row is gone, so a fixed
/// scratch key can't be reused across invocations. Instead each method consumes keys from a large pool
/// (re-created in <c>[GlobalSetup]</c> per benchmark method); <c>[IterationCleanup]</c> re-inserts only the
/// rows consumed in that iteration, outside the measured region. EF uses set-based <c>ExecuteDelete</c> so
/// every library issues a single DELETE statement.
/// </summary>
public class DeleteBenchmarks : BenchmarkBase
{
    private const string InsertPoolSql =
        "INSERT OR IGNORE INTO bench_product (id, name, category, price, quantity) VALUES (@id, 'del', 'del', 1, 1)";
    private const string DeleteSql = "DELETE FROM bench_product WHERE id = @id";
    private const int PoolSize = 16384;

    private Guid[] _pool = [];
    private int _index;

    public override async Task GlobalSetup()
    {
        await base.GlobalSetup();
        _pool = new Guid[PoolSize];
        for (var i = 0; i < PoolSize; i++)
        {
            _pool[i] = Guid.NewGuid();
        }

        await InsertPoolRangeAsync(0, PoolSize);
        _index = 0;
    }

    [IterationCleanup]
    public void RefillConsumed()
    {
        // Re-create only the rows consumed this iteration so the next iteration has keys to delete again.
        var consumed = Math.Min(_index, PoolSize);
        InsertPoolRangeAsync(0, consumed).AsTask().GetAwaiter().GetResult();
        _index = 0;
    }

    private Guid NextKey() => _index < _pool.Length ? _pool[_index++] : _pool[^1];

    private async ValueTask InsertPoolRangeAsync(int start, int count)
    {
        if (count <= 0)
        {
            return;
        }

        await using var connection = await Harness.OpenConnectionAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertPoolSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        command.Parameters.Add(parameter);

        for (var i = start; i < start + count; i++)
        {
            parameter.Value = _pool[i];
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task Dormant()
    {
        await using var session = await Harness.OpenDormantSessionAsync();
        await session.DeleteProduct(NextKey());
        await session.CommitAsync();
    }

    [Benchmark]
    public async Task Dapper()
    {
        await using var connection = await Harness.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SqlMapper.ExecuteAsync(connection, DeleteSql, new { id = NextKey() }, transaction);

        await transaction.CommitAsync();
    }

    [Benchmark]
    public async Task EfCore()
    {
        var id = NextKey();
        await using var context = Harness.NewEfContext();
        await context.Products.Where(p => p.Id == id).ExecuteDeleteAsync();
        await context.SaveChangesAsync();
    }
}
