using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 005 US2 (T036/SC-004): the dialect boundary is general — one authored unit, built into a neutral SqlIr,
// renders to a per-dialect variant by ISqlDialectRenderer. This proves no dialect lexical choice (JSON cast,
// placeholder form, schema/table qualification) is baked into the IR: each renderer adds its own.
public sealed class DialectBoundaryTests
{
    private const string Schema = """
        module catalog;

        entity Doc {
          id: uuid primary;
          data: json;
        }
        """;

    private const string Commands = """
        module catalog;

        mutation create_doc(id: uuid, data: json) {
          insert Doc d {
            d.id = id
            d.data = data
          }
        }
        """;

    private static string Run()
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/catalog.dqls", Schema),
            new TestAdditionalText("schema/catalog.dql", Commands));
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        return string.Join(
            "\n",
            driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Same_unit_renders_a_distinct_variant_per_dialect()
    {
        var generated = Run();

        // Both dialect arms are emitted from the one authored unit.
        await Assert.That(generated).Contains("global::Dormant.Abstractions.Providers.DialectId.PostgreSql =>");
        await Assert.That(generated).Contains("global::Dormant.Abstractions.Providers.DialectId.Sqlite =>");

        // PostgreSQL: schema-qualified table, $n placeholders, ::jsonb cast (dialect lexical choices).
        await Assert.That(generated).Contains("INSERT INTO \"catalog\".\"doc\" (\"id\", \"data\") VALUES ($1, $2::jsonb)");

        // SQLite: schema folded into the table name, @pN placeholders, NO cast — same neutral IR, different render.
        await Assert.That(generated).Contains("INSERT INTO \"catalog_doc\" (\"id\", \"data\") VALUES (@p1, @p2)");
    }

    [Test]
    public async Task No_dialect_specific_cast_leaks_into_the_other_dialect()
    {
        var generated = Run();

        // The PostgreSQL-only json cast must not appear on the SQLite placeholder (proves the cast is a
        // renderer decision, not an IR literal).
        await Assert.That(generated).DoesNotContain("@p2::jsonb");
        await Assert.That(generated).DoesNotContain("$2::jsonb::jsonb");
    }
}
