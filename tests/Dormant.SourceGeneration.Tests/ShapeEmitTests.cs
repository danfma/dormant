using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 009 US1: a root-object shape `select a { title, writer: { name } }` emits a JOIN-flattened SELECT,
// nested immutable records (shape = type), and a positional recursive materializer.
public sealed class ShapeEmitTests
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

        query article_card(id: uuid) {
          from Article a
          where a.id == id
          select a {
            title,
            writer: { name }
          }
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
    public async Task Shape_emits_nested_records_join_sql_and_materializer()
    {
        var generated = Run();

        // Nested immutable records: shape = type (to-one required ref ⇒ non-null nested member).
        await Assert
            .That(generated)
            .Contains(
                "public sealed record ArticleCardResult(string Title, ArticleCardResultWriter Writer);"
            );
        await Assert
            .That(generated)
            .Contains("public sealed record ArticleCardResultWriter(string Name);");

        // JOIN-flattened SELECT (PostgreSQL): root scalar, target-PK probe, nested scalar; INNER JOIN.
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"title\", \"writer\".\"id\", \"writer\".\"name\" "
                    + "FROM \"app\".\"article\" \"a\" "
                    + "INNER JOIN \"app\".\"author\" \"writer\" ON \"a\".\"writer_id\" = \"writer\".\"id\" "
                    + "WHERE \"a\".\"id\" = $1"
            );

        // SQLite variant: folded table names, @pN placeholder.
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"title\", \"writer\".\"id\", \"writer\".\"name\" "
                    + "FROM \"app_article\" \"a\" "
                    + "INNER JOIN \"app_author\" \"writer\" ON \"a\".\"writer_id\" = \"writer\".\"id\" "
                    + "WHERE \"a\".\"id\" = @p1"
            );

        // Recursive positional materializer (the probe column at ordinal 1 is selected but unread for a
        // required ref; the nested record reads the scalar at ordinal 2).
        await Assert
            .That(generated)
            .Contains(
                "static reader => new ArticleCardResult(reader.GetValue<string>(0), "
                    + "new ArticleCardResultWriter(reader.GetValue<string>(2)))"
            );
    }
}
