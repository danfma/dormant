using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US8 (T073, first slice): a `json` property maps to a build-time-known .NET representation (string) over
// a PostgreSQL `jsonb` column and round-trips through the no-boxing IO path (FR-038). The generated DDL
// declares the column `jsonb`, so the value is stored as real JSON (PG normalizes it). Native containment
// operators + STJ-typed jsonb + GIS are later US8 slices.
public sealed class JsonbTests
{
    [Test]
    public async Task Json_property_round_trips_through_a_jsonb_column()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var id = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateDocument(id, "{\"k\": \"v\", \"n\": 42}");
            await session.CommitAsync();
        }

        await using (var session = await factory.OpenSessionAsync())
        {
            var document = await session.GetAsync<Document>(id);
            await Assert.That(document).IsNotNull();
            // PG jsonb normalizes whitespace/key order, so assert structure rather than exact text.
            await Assert.That(document!.Data).Contains("\"k\"");
            await Assert.That(document.Data).Contains("\"v\"");
            await Assert.That(document.Data).Contains("42");
        }
    }
}
