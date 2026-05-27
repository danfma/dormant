using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 009 P-B (T014): a query that navigates a to-one reference in its WHERE (`a.writer.name`) emits a
// relational SELECT — root columns qualified by the root alias, a LEFT JOIN to the referenced table on
// the `<ref>_id` FK, and the predicate over the joined alias — rendered per dialect.
public sealed class NavigationEmitTests
{
    private const string Schema = """
        module app;

        entity Author {
          id: uuid primary;
          name: string;
        }

        entity Article {
          id: uuid primary;
          title: string;
          writer: Author;
        }
        """;

    private const string Queries = """
        module app;

        query articles_by_author(name: string) {
          from Article a
          where a.writer.name == name
          select a
        }
        """;

    private static string Run()
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/app.dqls", Schema),
            new TestAdditionalText("schema/app.dql", Queries)
        );
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var sources = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources);
        return string.Join("\n", sources.Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Navigation_emits_join_per_dialect()
    {
        var generated = Run();

        // PostgreSQL variant: schema-qualified tables, $n placeholders.
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"id\", \"a\".\"title\", \"a\".\"writer_id\" "
                    + "FROM \"app\".\"article\" \"a\" "
                    + "LEFT JOIN \"app\".\"author\" \"writer\" ON \"a\".\"writer_id\" = \"writer\".\"id\" "
                    + "WHERE \"writer\".\"name\" = $1"
            );

        // SQLite variant: schema folded into the table name, @pN placeholders.
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"id\", \"a\".\"title\", \"a\".\"writer_id\" "
                    + "FROM \"app_article\" \"a\" "
                    + "LEFT JOIN \"app_author\" \"writer\" ON \"a\".\"writer_id\" = \"writer\".\"id\" "
                    + "WHERE \"writer\".\"name\" = @p1"
            );
    }
}
