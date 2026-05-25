using Dormant.Abstractions.Querying;
using Dormant.Abstractions.Sessions;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US2 (T037): two sessions load the same row; the first commit wins (token 0 → 1); the second commit
// matches the now-stale token in its WHERE clause, affects 0 rows, and surfaces a
// ConcurrencyConflictException rather than silently overwriting (FR-015).
public sealed class ConcurrencyTests
{
    [Test]
    public async Task Stale_write_raises_concurrency_conflict()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        await using (var dataSource = DormantPostgres.CreateDataSource(connectionString))
        await using (var db = await dataSource.OpenAsync())
        {
            await db.ExecuteAsync(new PreparedStatement(
                "CREATE TABLE \"Account\" (\"id\" uuid primary key, \"balance\" integer not null, \"version\" integer not null)"));
        }

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.AddAsync(new Account { Id = id, Balance = 100, Version = 0 });
            await session.CommitAsync();
        }

        // Both sessions load the same committed row (version 0) before either commits.
        await using var session1 = await factory.OpenSessionAsync();
        await using var session2 = await factory.OpenSessionAsync();

        var account1 = await session1.GetAsync<Account>(id);
        var account2 = await session2.GetAsync<Account>(id);
        await Assert.That(account1).IsNotNull();
        await Assert.That(account2).IsNotNull();

        // First writer wins: version 0 → 1.
        account1!.Balance = 150;
        await session1.CommitAsync();

        // Second writer is stale (WHERE version = 0 matches no row) → conflict.
        account2!.Balance = 200;
        ConcurrencyConflictException? caught = null;
        try
        {
            await session2.CommitAsync();
        }
        catch (ConcurrencyConflictException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();

        // The first writer's value persists; the stale write never landed.
        await using var verify = await factory.OpenSessionAsync();
        var persisted = await verify.GetAsync<Account>(id);
        await Assert.That(persisted!.Balance).IsEqualTo(150);
        await Assert.That(persisted.Version).IsEqualTo(1);
    }
}
