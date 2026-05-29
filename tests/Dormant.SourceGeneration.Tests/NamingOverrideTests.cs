using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US9 (T111): a per-unit db("…") override wins over the active convention for that single identifier,
// while sibling identifiers still follow the convention (FR-054).
public sealed class NamingOverrideTests
{
    private const string Schema = """
        module shop;

        entity RecentPost db("posts") {
          id: Uuid { constraint primary; }
          createdAt: DateTime { annotation column("created"); }
          title: String;
        }
        """;

    [Test]
    public async Task Per_unit_override_wins_over_convention()
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/shop.dqls", Schema)
        );
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var generated = string.Join(
            "\n",
            driver
                .GetRunResult()
                .Results.SelectMany(r => r.GeneratedSources)
                .Select(s => s.SourceText.ToString())
        );

        // Table override "posts" and column override "created" win; "title" still follows snake_case.
        await Assert.That(generated).Contains("CREATE TABLE IF NOT EXISTS \"shop\".\"posts\"");
        await Assert.That(generated).Contains("\"created\"");
    }
}
