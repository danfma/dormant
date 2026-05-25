using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US9 (T112): when the active convention collapses two distinct members to the same database column
// name, the build reports a source-located ORM013 collision diagnostic rather than ambiguous SQL (FR-057).
public sealed class NamingDiagnosticTests
{
    [Test]
    public async Task Snake_case_collision_reports_ORM013()
    {
        // Both `userId` and `user_id` resolve to `user_id` under snake_case.
        const string schema = """
            module shop;

            entity Thing {
              id: uuid primary;
              userId: int;
              user_id: int;
            }
            """;

        var driver = GeneratorTestHarness.CreateDriver(new TestAdditionalText("schema/shop.dqls", schema));
        var result = driver.RunGenerators(CSharpCompilation.Create("Tests")).GetRunResult();

        await Assert.That(result.Diagnostics.Any(d => d.Id == "ORM013")).IsTrue();
    }

    [Test]
    public async Task Distinct_names_do_not_collide()
    {
        const string schema = """
            module shop;

            entity Thing {
              id: uuid primary;
              userId: int;
              tenantId: int;
            }
            """;

        var driver = GeneratorTestHarness.CreateDriver(new TestAdditionalText("schema/shop.dqls", schema));
        var result = driver.RunGenerators(CSharpCompilation.Create("Tests")).GetRunResult();

        await Assert.That(result.Diagnostics.Any(d => d.Id == "ORM013")).IsFalse();
    }
}
