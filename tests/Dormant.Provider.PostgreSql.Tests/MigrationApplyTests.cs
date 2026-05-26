using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US5 (T059): applying the generated initial schema creates the module schema + tables (schema-qualified,
// FR-020/FR-045), is idempotent (IF NOT EXISTS), and the resulting tables are immediately usable.
public sealed class MigrationApplyTests
{
    [Test]
    public async Task EnsureCreated_is_idempotent_and_creates_usable_schema()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        // Apply twice — second run must not fail (CREATE SCHEMA/TABLE IF NOT EXISTS).
        await DormantPostgres.EnsureCreatedAsync(connectionString);
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        // The generated, schema-qualified table is immediately usable.
        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateWidget(id, "applied", 3);
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var widget = await session.GetAsync<Widget>(id);
            await Assert.That(widget).IsNotNull();
            await Assert.That(widget!.Name).IsEqualTo("applied");
        }
    }
}
