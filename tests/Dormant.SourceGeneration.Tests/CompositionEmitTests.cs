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
          id: uuid { constraint primary; }
          name: string;
        }

        entity Article {
          id: uuid { constraint primary; }
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

        query feed_dto(id: uuid) {
          from Article a
          where a.id == id
          select { headline = a.title, authorName = a.writer.name } into App.FeedDto
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

    [Test]
    public async Task Into_materializes_user_record()
    {
        var generated = Run();

        // The method returns the user-owned type; no generated result record is emitted for it.
        await Assert
            .That(generated)
            .Contains(
                "global::System.Collections.Generic.IAsyncEnumerable<global::App.FeedDto> FeedDto("
            );
        await Assert
            .That(generated)
            .Contains(
                "static reader => new global::App.FeedDto(reader.GetValue<string>(0), reader.GetValue<string>(1))"
            );
        await Assert.That(generated).DoesNotContain("record FeedDtoResult");
    }
}
