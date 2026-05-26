using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// 002: authored insert command + primary-key load round-trip against real PostgreSQL (Testcontainers).
// Exercises the no-reflection materializer, the command's INSERT … RETURNING, the binding registry, and the
// thin session. (delete is a `delete` command in a later slice; change-tracking is gone.)
public sealed class CrudTests
{
    [Test]
    public async Task Insert_command_then_get_by_key_round_trips()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateWidget(id, "gizmo", 7);
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var widget = await session.GetAsync<Widget>(id);

            await Assert.That(widget).IsNotNull();
            await Assert.That(widget!.Name).IsEqualTo("gizmo");
            await Assert.That(widget.Quantity).IsEqualTo(7);
        }
    }
}
