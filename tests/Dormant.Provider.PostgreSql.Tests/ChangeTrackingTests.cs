using Dormant.Abstractions.Querying;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US2 (T035): change-tracking commit updates only the columns that changed since load (FR-014). Proven
// behaviorally: an out-of-band write to a column the session never touches must survive the commit — an
// all-columns UPDATE would revert it; a changed-columns-only UPDATE leaves it intact.
public sealed class ChangeTrackingTests
{
    [Test]
    public async Task Modify_one_field_then_commit_updates_only_that_column()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();

        await using (var dataSource = DormantPostgres.CreateDataSource(connectionString))
        await using (var db = await dataSource.OpenAsync())
        {
            await db.ExecuteAsync(new PreparedStatement(
                "CREATE TABLE \"widget\" (\"id\" uuid primary key, \"name\" text not null, \"quantity\" integer not null)"));
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
            // Load → snapshot captures quantity = 7.
            var widget = await session.GetAsync<Widget>(id);
            await Assert.That(widget).IsNotNull();

            // Out-of-band writer (separate connection/transaction) changes the untouched column to 99.
            await using (var other = DormantPostgres.CreateDataSource(connectionString))
            await using (var otherDb = await other.OpenAsync())
            {
                await otherDb.BeginAsync();
                await otherDb.ExecuteAsync(new PreparedStatement(
                    "UPDATE \"widget\" SET \"quantity\" = $1 WHERE \"id\" = $2",
                    writer =>
                    {
                        writer.Write(1, 99);
                        writer.Write(2, id);
                    }));
                await otherDb.CommitAsync();
            }

            // Change only the name and commit; quantity must NOT be part of the UPDATE.
            widget!.Name = "gadget";
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var widget = await session.GetAsync<Widget>(id);
            await Assert.That(widget!.Name).IsEqualTo("gadget");
            // 99 (the out-of-band value) survives → only the name column was updated, not quantity.
            await Assert.That(widget.Quantity).IsEqualTo(99);
        }
    }
}
