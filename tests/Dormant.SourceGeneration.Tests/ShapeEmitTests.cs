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

    private const string ToManySchema = """
        module app;

        entity Article {
          id: uuid primary;
          title: string;
          tags: Set<Tag>;
        }

        entity Tag {
          id: uuid primary;
          label: string;
          article: Article;
        }
        """;

    private const string ToManyQueries = """
        module app;

        query article_tags(id: uuid) {
          from Article a
          where a.id == id
          select a {
            title,
            tags: { label }
          }
        }
        """;

    private static string Run() => Run(Schema, Queries);

    private static string Run(string schema, string queries)
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/app.dqls", schema),
            new TestAdditionalText("schema/app.dql", queries)
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

    [Test]
    public async Task To_many_shape_emits_json_aggregation_and_parser()
    {
        var generated = Run(ToManySchema, ToManyQueries);

        // Nested records: to-many ⇒ IReadOnlyList member.
        await Assert
            .That(generated)
            .Contains(
                "public sealed record ArticleTagsResult(string Title, global::System.Collections.Generic.IReadOnlyList<ArticleTagsResultTags> Tags);"
            );
        await Assert
            .That(generated)
            .Contains("public sealed record ArticleTagsResultTags(string Label);");

        // PostgreSQL: jsonb_agg(jsonb_build_object(...)) correlated subquery on the backlink FK.
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"title\", (SELECT coalesce(jsonb_agg(jsonb_build_object('label', \"tags\".\"label\")), '[]'::jsonb) "
                    + "FROM \"app\".\"tag\" \"tags\" WHERE \"tags\".\"article_id\" = \"a\".\"id\") "
                    + "FROM \"app\".\"article\" \"a\" WHERE \"a\".\"id\" = $1"
            );

        // SQLite: json_group_array(json_object(...)).
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"title\", (SELECT coalesce(json_group_array(json_object('label', \"tags\".\"label\")), json('[]')) "
                    + "FROM \"app_tag\" \"tags\" WHERE \"tags\".\"article_id\" = \"a\".\"id\") "
                    + "FROM \"app_article\" \"a\" WHERE \"a\".\"id\" = @p1"
            );

        // Emitted JsonDocument-based parser + the materializer call.
        await Assert
            .That(generated)
            .Contains(
                "global::System.Collections.Generic.IReadOnlyList<ArticleTagsResultTags> ParseArticleTagsResultTags(string json)"
            );
        await Assert
            .That(generated)
            .Contains(
                "static reader => new ArticleTagsResult(reader.GetValue<string>(0), ParseArticleTagsResultTags(reader.GetValue<string>(1)))"
            );
    }
}
