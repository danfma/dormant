using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 009 US2: a free composition `select { headline = a.title, authorName = a.writer.name }` emits a flat
// record of named members, a JOIN-flattened SELECT (navigation generates the join), and a positional
// materializer.
public sealed class CompositionEmitTests
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

        query feed_item(id: uuid) {
          from Article a
          where a.id == id
          select { headline = a.title, authorName = a.writer.name }
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
    public async Task Composition_emits_named_record_join_and_materializer()
    {
        var generated = Run();

        await Assert
            .That(generated)
            .Contains("public sealed record FeedItemResult(string Headline, string AuthorName);");

        await Assert
            .That(generated)
            .Contains(
                "SELECT \"a\".\"title\", \"writer\".\"name\" "
                    + "FROM \"app\".\"article\" \"a\" "
                    + "INNER JOIN \"app\".\"author\" \"writer\" ON \"a\".\"writer_id\" = \"writer\".\"id\" "
                    + "WHERE \"a\".\"id\" = $1"
            );

        await Assert
            .That(generated)
            .Contains(
                "static reader => new FeedItemResult(reader.GetValue<string>(0), reader.GetValue<string>(1))"
            );
    }
}
