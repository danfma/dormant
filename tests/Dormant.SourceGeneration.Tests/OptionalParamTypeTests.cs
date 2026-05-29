using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US4 (T069): a query with optional parameters generates ONE result type and nullable parameters with
// default values; the SQL is assembled at runtime by fragment selection (FR-012/031, SC-005).
public sealed class OptionalParamTypeTests
{
    private const string Schema = """
        module catalog;

        entity Widget {
          id: Uuid { constraint primary; }
          name: String;
          quantity: Int;
        }
        """;

    private const string Queries = """
        module catalog;

        query search_widgets(minQuantity: optional Int, name: optional String) {
          from Widget w
          where w.quantity >= minQuantity && w.name == name
          order by w.name asc
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
        return string.Join(
            "\n",
            driver
                .GetRunResult()
                .Results.SelectMany(r => r.GeneratedSources)
                .Select(s => s.SourceText.ToString())
        );
    }

    [Test]
    public async Task Optional_params_are_nullable_with_one_result_type()
    {
        var generated = Run();

        // Single result type (Widget), optional params nullable + defaulted.
        await Assert
            .That(generated)
            .Contains(
                "public global::System.Collections.Generic.IAsyncEnumerable<Widget> SearchWidgets(int? minQuantity = default, string? name = default,"
            );
        // Runtime fragment selection — each optional filter included only when its parameter is supplied.
        await Assert.That(generated).Contains("if (minQuantity != null) { conds.Add(");
        await Assert.That(generated).Contains("if (name != null) { conds.Add(");
        await Assert
            .That(generated)
            .Contains(
                "if (conds.Count > 0) { sql.Append(\" WHERE \").Append(string.Join(\" AND \", conds)); }"
            );
        // Value-type optional unwrapped via .Value when binding.
        await Assert
            .That(generated)
            .Contains("if (minQuantity != null) { writer.Write(++i, minQuantity.Value); }");
    }
}
