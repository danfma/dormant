using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// 002 US6 (T031): optimistic concurrency expressed in an authored `update` command — the version is
// matched in the filter; a stale version affects 0 rows (the caller's conflict signal) while the first
// writer wins. update/delete commands return the affected-row count. (FR-011)
public sealed class CommandConcurrencyTests
{
    [Test]
    public async Task Update_command_with_version_filter_detects_stale_write()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateAccount(id, 100, 0);
            await session.CommitAsync();
        }

        // First writer wins: version 0 → 1.
        await using (var session = await factory.OpenSessionAsync())
        {
            var affected = await session.BumpAccountBalance(id, 150, 0, 1);
            await session.CommitAsync();
            await Assert.That(affected).IsEqualTo(1);
        }

        // Second writer is stale (where a.version == 0 matches no row) → 0 affected.
        await using (var session = await factory.OpenSessionAsync())
        {
            var affected = await session.BumpAccountBalance(id, 200, 0, 1);
            await session.CommitAsync();
            await Assert.That(affected).IsEqualTo(0);
        }

        // First writer's value persists.
        await using (var session = await factory.OpenSessionAsync())
        {
            var account = await session.GetAsync<Account>(id);
            await Assert.That(account!.Balance).IsEqualTo(150);
            await Assert.That(account.Version).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Delete_command_removes_the_row()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateWidget(id, "doomed", 1);
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var affected = await session.DeleteWidget(id);
            await session.CommitAsync();
            await Assert.That(affected).IsEqualTo(1);
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var widget = await session.GetAsync<Widget>(id);
            await Assert.That(widget).IsNull();
        }
    }
}
