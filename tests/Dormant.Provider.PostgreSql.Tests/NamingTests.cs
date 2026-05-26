using Dormant.Abstractions.Querying;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US9 (T113): the default snake_case convention is applied end-to-end against real PostgreSQL. A
// multi-word entity (StockItem) and member (itemName) round-trip through the snake_case table/column
// names `stock_item` / `item_name` (FR-055, SC-015).
public sealed class NamingTests
{
    [Test]
    public async Task Snake_case_table_and_column_round_trip()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        // Schema/table created by the generated DDL (schema-qualified, snake_case).
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateStockItem(id, "bolt");
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var item = await session.GetAsync<StockItem>(id);
            await Assert.That(item).IsNotNull();
            await Assert.That(item!.ItemName).IsEqualTo("bolt");
        }
    }
}
