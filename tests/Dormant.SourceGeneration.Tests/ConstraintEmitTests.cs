using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// Feature 012 (Slice 2): member-level constraints lower to named table-level DDL constraints.
// `unique` → UNIQUE; numeric bounds + length → CHECK. Names come from `as` or a deterministic default.
public sealed class ConstraintEmitTests
{
    private const string Schema = """
        module shop;

        entity Product {
          id: uuid { constraint primary; }
          sku: str {
            constraint unique as products_sku_unique;
            constraint max_length(32);
          }
          price: int { constraint range(min = 0, max = 1000000); }
          name: str { constraint min_length(1); }
        }
        """;

    private static string Generate()
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/shop.dqls", Schema)
        );
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var sources = driver.GetRunResult().Results[0].GeneratedSources;
        return string.Join("\n", sources.Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Member_unique_emits_named_unique_constraint()
    {
        var generated = Generate();
        // Explicit `as` name is honored.
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"products_sku_unique\" UNIQUE (\"sku\")");
    }

    [Test]
    public async Task Length_and_range_emit_check_constraints_with_default_names()
    {
        var generated = Generate();
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"product_sku_maxlen\" CHECK (length(\"sku\") <= 32)");
        await Assert
            .That(generated)
            .Contains(
                "CONSTRAINT \"product_price_range\" CHECK (\"price\" >= 0 AND \"price\" <= 1000000)"
            );
        await Assert
            .That(generated)
            .Contains("CONSTRAINT \"product_name_minlen\" CHECK (length(\"name\") >= 1)");
    }

    [Test]
    public async Task Primary_stays_inline_on_the_column()
    {
        var generated = Generate();
        // Single-column primary remains an inline column constraint, not a table constraint.
        await Assert.That(generated).Contains("\"id\" uuid NOT NULL PRIMARY KEY");
        await Assert.That(generated).DoesNotContain("PRIMARY KEY (\"id\")");
    }
}
