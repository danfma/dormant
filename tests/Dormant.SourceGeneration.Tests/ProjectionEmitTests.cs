using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US3 (T048/T050): a flat projection compiles to a distinct record with EXACTLY the requested members,
// and a full-entity query compiles to an ISession extension carrying build-time SQL. Because the record
// omits non-projected members, referencing them cannot compile (the negative-test guarantee, FR-008).
public sealed class ProjectionEmitTests
{
    private const string Schema = """
        module catalog;

        entity Widget {
          id: uuid primary;
          name: str;
          quantity: int;
        }
        """;

    private const string Queries = """
        module catalog;

        query widget_names(min: int) {
          from Widget w
          where w.quantity >= min
          order by w.name asc
          select {
            w.id
            w.name
          }
        }

        query all_widgets(min: int) {
          from Widget w
          where w.quantity >= min
          order by w.quantity desc
          select w
        }
        """;

    private static string Run()
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/catalog.dqls", Schema),
            new TestAdditionalText("schema/catalog.dql", Queries)
        );
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var sources = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources);
        return string.Join("\n", sources.Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Projection_emits_distinct_record_with_exactly_requested_members()
    {
        var generated = Run();

        // Exactly id + name — proving non-projected members (e.g. quantity) are absent → uncompilable (T050).
        await Assert
            .That(generated)
            .Contains(
                "public sealed record WidgetNamesResult(global::System.Guid Id, string Name);"
            );

        // Emitted inside a C# 14 extension block (FR-058) — receiver `session`, no `this` parameter.
        await Assert
            .That(generated)
            .Contains("extension(global::Dormant.Abstractions.Sessions.ISession session)");
        await Assert
            .That(generated)
            .Contains(
                "public global::System.Collections.Generic.IAsyncEnumerable<WidgetNamesResult> WidgetNames(int min,"
            );
    }

    [Test]
    public async Task Full_entity_query_emits_session_extension_with_build_time_sql()
    {
        var generated = Run();

        await Assert.That(generated).Contains("public static partial class CatalogQueries");
        await Assert
            .That(generated)
            .Contains(
                "public global::System.Collections.Generic.IAsyncEnumerable<Widget> AllWidgets(int min,"
            );
        // Build-time SQL: full-entity column list in declaration order + filter + order by.
        await Assert
            .That(generated)
            .Contains(
                "SELECT \"id\", \"name\", \"quantity\" FROM \"catalog\".\"widget\" WHERE \"quantity\" >= $1 ORDER BY \"quantity\" DESC"
            );
        await Assert.That(generated).Contains("static reader => new Widget(reader)");
    }

    [Test]
    public async Task Generation_is_deterministic()
    {
        await Assert.That(Run()).IsEqualTo(Run());
    }
}
