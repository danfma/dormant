using System.Collections.Generic;
using Dormant.Abstractions.Sessions;
using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US4 (T068): one query with two optional filters — supplying none, one, or both changes the executed
// SQL (fragment selection) but never the result type, and filters correctly each way (FR-012/031, SC-005).
public sealed class OptionalParamsTests
{
    [Test]
    public async Task Optional_filters_none_one_both_same_type_correct_rows()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        await using (var session = await factory.OpenSessionAsync())
        {
            await session.CreateWidget(Guid.NewGuid(), "five", 5);
            await session.CreateWidget(Guid.NewGuid(), "seven", 7);
            await session.CreateWidget(Guid.NewGuid(), "nine", 9);
            await session.CommitAsync();
        }

        // Each combination filters correctly; ordered by name asc. (Joined string also verifies order.)
        await Assert.That(await NamesAsync(factory, null, null)).IsEqualTo("five,nine,seven"); // none
        await Assert.That(await NamesAsync(factory, 6, null)).IsEqualTo("nine,seven");          // min only
        await Assert.That(await NamesAsync(factory, null, "seven")).IsEqualTo("seven");         // name only
        await Assert.That(await NamesAsync(factory, 6, "nine")).IsEqualTo("nine");              // both
    }

    private static async Task<string> NamesAsync(ISessionFactory factory, int? minQuantity, string? name)
    {
        var names = new List<string>();
        await using var session = await factory.OpenSessionAsync();
        await foreach (var widget in session.SearchWidgets(minQuantity, name))
        {
            names.Add(widget.Name);
        }

        return string.Join(",", names);
    }
}
