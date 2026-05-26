using Dormant.Provider.PostgreSql;
using Dormant.Provider.PostgreSql.Tests.Schema.Catalog;
using Npgsql;
using Testcontainers.PostgreSql;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Provider.PostgreSql.Tests;

// 003 T043 (FR-021/FR-022): a `with`-block mutation runs each binding as its own SQL statement within the
// session transaction (no CTE), flowing the bound Author's id into the Article's `writer_id` foreign key.
public sealed class WithBlockTests
{
    [Test]
    public async Task With_block_flows_bound_author_id_into_article_fk()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DormantPostgres.EnsureCreatedAsync(connectionString);

        await using var factory = DormantPostgres.CreateSessionFactory(connectionString);
        var authorId = Guid.NewGuid();
        var articleId = Guid.NewGuid();

        await using (var session = await factory.OpenSessionAsync())
        {
            // with a = (insert Author …) ; insert Article r { r.writer = a } returning r.id
            var returnedId = await session.CreateAuthorWithArticle(
                authorId,
                "Ada",
                articleId,
                "Notes"
            );
            await Assert.That(returnedId).IsEqualTo(articleId);
            await session.CommitAsync();
        }

        // The bound author's id flowed into the article's `writer_id` FK column.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT writer_id FROM catalog.article WHERE id = @id",
            connection
        );
        command.Parameters.AddWithValue("id", articleId);
        var writerId = (Guid)(await command.ExecuteScalarAsync())!;
        await Assert.That(writerId).IsEqualTo(authorId);
    }
}
