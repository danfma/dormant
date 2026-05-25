using Dormant.Abstractions.Querying;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US2 (T034): a generated entity binding + session insert + primary-key load round-trip against a
// real PostgreSQL (Testcontainers). Exercises the no-reflection materializer, prebuilt INSERT/SELECT,
// the module-init binding registry, and the unit-of-work session.
public sealed class CrudTests
{
    [Test]
    public async Task Insert_then_get_by_key_round_trips()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        await using (var dataSource = DormantPostgres.CreateDataSource(connectionString))
        await using (var db = await dataSource.OpenAsync())
        {
            await db.ExecuteAsync(new PreparedStatement(
                "CREATE TABLE \"Widget\" (\"id\" uuid primary key, \"name\" text not null, \"quantity\" integer not null)"));
        }

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.AddAsync(new Widget { Id = id, Name = "gizmo", Quantity = 7 });
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
