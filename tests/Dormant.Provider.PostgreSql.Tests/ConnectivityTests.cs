using System.Collections.Generic;
using Dormant.Abstractions.Querying;
using Dormant.Provider.PostgreSql;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// US2 foundation: the PostgreSQL adapter round-trips through the no-boxing IO path against a real
// PostgreSQL provisioned in ephemeral Docker (spec Clarifications: Testcontainers, never mocks).
public sealed class ConnectivityTests
{
    [Test]
    public async Task No_boxing_io_round_trips_against_real_postgres()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();

        await using var dataSource = DormantPostgres.CreateDataSource(
            postgres.GetConnectionString()
        );

        await using (var session = await dataSource.OpenAsync())
        {
            await session.BeginAsync();
            await session.ExecuteAsync(
                new PreparedStatement("CREATE TABLE t (n integer not null, label text not null)")
            );
            await session.ExecuteAsync(
                new PreparedStatement(
                    "INSERT INTO t(n, label) VALUES ($1, $2)",
                    writer =>
                    {
                        writer.Write(1, 42);
                        writer.Write(2, "answer");
                    }
                )
            );
            await session.CommitAsync();
        }

        var rows = new List<(int N, string Label)>();
        await using (var session = await dataSource.OpenAsync())
        {
            await foreach (
                var row in session.QueryAsync(
                    new PreparedStatement("SELECT n, label FROM t ORDER BY n"),
                    reader => (reader.GetValue<int>(0), reader.GetValue<string>(1))
                )
            )
            {
                rows.Add(row);
            }
        }

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].N).IsEqualTo(42);
        await Assert.That(rows[0].Label).IsEqualTo("answer");
    }
}
