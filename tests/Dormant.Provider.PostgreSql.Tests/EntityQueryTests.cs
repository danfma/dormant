using System.Collections.Generic;
using Dormant.Abstractions.Querying;
using Dormant.Abstractions.Sessions;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US3 (T051): a generated DSL query runs against real PostgreSQL. Full-entity queries populate every
// mapped column; flat projections return the distinct generated record. Build-time SQL, no boxing.
public sealed class EntityQueryTests
{
    [Test]
    public async Task Full_entity_query_populates_all_columns_filtered_and_ordered()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        await using var factory = await SeedAsync(postgres.GetConnectionString());

        var results = new List<Widget>();
        await using (var session = await factory.OpenSessionAsync())
        {
            await foreach (var widget in session.WidgetsByMinQuantity(6))
            {
                results.Add(widget);
            }
        }

        // quantity >= 6, ordered desc → [9, 7]; all columns populated.
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Quantity).IsEqualTo(9);
        await Assert.That(results[0].Name).IsEqualTo("nine");
        await Assert.That(results[1].Quantity).IsEqualTo(7);
        await Assert.That(results[1].Name).IsEqualTo("seven");
    }

    [Test]
    public async Task Flat_projection_query_returns_distinct_record()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        await using var factory = await SeedAsync(postgres.GetConnectionString());

        var names = new List<WidgetNamesResult>();
        await using (var session = await factory.OpenSessionAsync())
        {
            await foreach (var row in session.WidgetNames(0))
            {
                names.Add(row);
            }
        }

        // ordered by name asc: five, nine, seven.
        await Assert.That(names.Count).IsEqualTo(3);
        await Assert.That(names[0].Name).IsEqualTo("five");
        await Assert.That(names[1].Name).IsEqualTo("nine");
        await Assert.That(names[2].Name).IsEqualTo("seven");
    }

    private static async Task<ISessionFactory> SeedAsync(string connectionString)
    {
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        var factory = DormantPostgres.CreateSessionFactory(connectionString);
        await using (var session = await factory.OpenSessionAsync())
        {
            await session.AddAsync(new Widget { Id = Guid.NewGuid(), Name = "five", Quantity = 5 });
            await session.AddAsync(new Widget { Id = Guid.NewGuid(), Name = "seven", Quantity = 7 });
            await session.AddAsync(new Widget { Id = Guid.NewGuid(), Name = "nine", Quantity = 9 });
            await session.CommitAsync();
        }

        return factory;
    }
}
