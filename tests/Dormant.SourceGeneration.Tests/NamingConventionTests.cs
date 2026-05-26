using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US9 (T109/T110): database identifiers follow the active naming convention — snake_case by default
// (FR-052), switchable per project (FR-053). PascalCase entity + camelCase member demonstrate the
// transform (RecentPost → recent_post, createdAt → created_at).
public sealed class NamingConventionTests
{
    private const string Schema = """
        module shop;

        entity RecentPost {
          id: uuid primary;
          createdAt: datetime;
          title: str;
        }
        """;

    private static string Run(Dictionary<string, string>? options = null)
    {
        var schemaText = new TestAdditionalText("schema/shop.dqls", Schema);
        var driver = options is null
            ? GeneratorTestHarness.CreateDriver(schemaText)
            : GeneratorTestHarness.CreateDriver(options, schemaText);
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var sources = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources);
        return string.Join("\n", sources.Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Default_convention_is_snake_case()
    {
        var generated = Run();

        // Table and columns are snake_case in the prebuilt CREATE TABLE DDL (schema-qualified, module → schema).
        await Assert
            .That(generated)
            .Contains("CREATE TABLE IF NOT EXISTS \"shop\".\"recent_post\"");
        await Assert.That(generated).Contains("\"created_at\"");
        // C# member names are unaffected (PascalCase).
        await Assert
            .That(generated)
            .Contains("public required global::System.DateTime CreatedAt { get; init; }");
    }

    [Test]
    public async Task Verbatim_convention_preserves_authored_identifiers()
    {
        var generated = Run(
            new Dictionary<string, string>
            {
                ["build_property.DormantNamingConvention"] = "verbatim",
            }
        );

        await Assert.That(generated).Contains("CREATE TABLE IF NOT EXISTS \"shop\".\"RecentPost\"");
        await Assert.That(generated).Contains("\"createdAt\"");
    }
}
