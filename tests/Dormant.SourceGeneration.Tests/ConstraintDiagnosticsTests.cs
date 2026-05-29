using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// Feature 012: constraint/annotation diagnostics (ORM029 unknown constraint, ORM030 type mismatch,
// ORM031 unknown member, ORM035 removed modifier, ORM036 invalid annotation).
public sealed class ConstraintDiagnosticsTests
{
    private static string[] DiagnosticIds(string schema)
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/diag.dqls", schema)
        );
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        return driver
            .GetRunResult()
            .Results.SelectMany(r => r.Diagnostics)
            .Select(d => d.Id)
            .ToArray();
    }

    [Test]
    public async Task Unknown_constraint_reports_ORM029()
    {
        var ids = DiagnosticIds(
            "module d;\nentity E { id: Uuid { constraint primary; } a: String { constraint bogus; } }"
        );
        await Assert.That(ids).Contains("ORM029");
    }

    [Test]
    public async Task Type_incompatible_constraint_reports_ORM030()
    {
        // max_length on an int member.
        var ids = DiagnosticIds(
            "module d;\nentity E { id: Uuid { constraint primary; } n: Int { constraint max_length(5); } }"
        );
        await Assert.That(ids).Contains("ORM030");
    }

    [Test]
    public async Task Entity_constraint_unknown_member_reports_ORM031()
    {
        var ids = DiagnosticIds(
            "module d;\nentity E { id: Uuid { constraint primary; } a: String; constraint unique on (a, ghost); }"
        );
        await Assert.That(ids).Contains("ORM031");
    }

    [Test]
    public async Task Removed_modifier_reports_ORM035()
    {
        var ids = DiagnosticIds("module d;\nentity E { id: Uuid primary; }");
        await Assert.That(ids).Contains("ORM035");
    }

    [Test]
    public async Task Scalar_with_unknown_base_reports_ORM033()
    {
        var ids = DiagnosticIds(
            "module d;\nscalar Bad extending Nope { constraint min_length(1); }\nentity E { id: Uuid { constraint primary; } }"
        );
        await Assert.That(ids).Contains("ORM033");
    }

    [Test]
    public async Task Unknown_annotation_reports_ORM036()
    {
        var ids = DiagnosticIds(
            "module d;\nentity E { id: Uuid { constraint primary; } a: String { annotation foo(\"x\"); } }"
        );
        await Assert.That(ids).Contains("ORM036");
    }
}
