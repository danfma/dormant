using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 003 US5 (FR-015/FR-009): removed 002 forms produce a located migration diagnostic (ORM020) instead of a
// generic parse error, and 003 alias/qualification rules are enforced (ORM021/022/024). The schema is shared.
public sealed class GrammarDiagnosticsTests
{
    private const string Schema = """
        module catalog;

        entity Widget {
          id: uuid { constraint primary; }
          name: str;
          quantity: int;
        }
        """;

    private static string[] DiagnosticIds(string units)
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/catalog.dqls", Schema),
            new TestAdditionalText("schema/catalog.dql", units)
        );
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        return driver
            .GetRunResult()
            .Results.SelectMany(r => r.Diagnostics)
            .Select(d => d.Id)
            .ToArray();
    }

    [Test]
    public async Task Removed_command_keyword_reports_ORM020()
    {
        var ids = DiagnosticIds(
            "module catalog;\ncommand CreateWidget(id: uuid) = insert Widget { id := id };"
        );
        await Assert.That(ids).Contains("ORM020");
    }

    [Test]
    public async Task Removed_equals_query_form_reports_ORM020()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery widgets(min: int) = select Widget filter .quantity >= min;"
        );
        await Assert.That(ids).Contains("ORM020");
    }

    [Test]
    public async Task Leading_dot_member_reports_ORM020()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery widgets(min: int) { from Widget w where .quantity >= min select w }"
        );
        await Assert.That(ids).Contains("ORM020");
    }

    [Test]
    public async Task Keyword_connective_and_reports_ORM020()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery widgets(min: int, n: string) { from Widget w where w.quantity >= min and w.name == n select w }"
        );
        await Assert.That(ids).Contains("ORM020");
    }

    [Test]
    public async Task Missing_alias_reports_ORM021()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery widgets(min: int) { from Widget where Widget.quantity >= min select Widget }"
        );
        await Assert.That(ids).Contains("ORM021");
    }

    [Test]
    public async Task Unqualified_member_reports_ORM024()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery widgets(min: int) { from Widget w where quantity >= min select w }"
        );
        await Assert.That(ids).Contains("ORM024");
    }

    [Test]
    public async Task Undeclared_alias_reports_ORM022()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery widgets(min: int) { from Widget w where x.quantity >= min select w }"
        );
        await Assert.That(ids).Contains("ORM022");
    }

    [Test]
    public async Task Duplicate_composition_member_reports_ORM028()
    {
        var ids = DiagnosticIds(
            "module catalog;\nquery dup(p: int) { from Widget w where w.quantity == p select { label = w.name, label = w.quantity } }"
        );
        await Assert.That(ids).Contains("ORM028");
    }
}
