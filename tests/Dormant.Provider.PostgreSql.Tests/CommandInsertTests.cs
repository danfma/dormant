using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// 002 US1 (T010): an authored DQL `insert` command compiles to an ISession method (build-time SQL) that
// writes exactly one row via INSERT … RETURNING and returns the materialized result — no Add/Save API.
public sealed class CommandInsertTests
{
    [Test]
    public async Task Authored_insert_command_writes_one_row_and_returns_it()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            // Generated command method: INSERT … RETURNING → materialized Widget.
            var created = await session.CreateWidget(id, "gizmo", 7);
            await Assert.That(created.Id).IsEqualTo(id);
            await Assert.That(created.Name).IsEqualTo("gizmo");
            await Assert.That(created.Quantity).IsEqualTo(7);
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var loaded = await session.GetAsync<Widget>(id);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.Name).IsEqualTo("gizmo");
            await Assert.That(loaded.Quantity).IsEqualTo(7);
        }
    }
}
