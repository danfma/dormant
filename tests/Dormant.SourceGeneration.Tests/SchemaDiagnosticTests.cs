using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US1 (T025): invalid schemas produce located diagnostics and no masking output.
public sealed class SchemaDiagnosticTests
{
    private static GeneratorDriverRunResult Run(string schema)
    {
        var driver = GeneratorTestHarness.CreateDriver(new TestAdditionalText("schema/app.dqls", schema));
        return driver.RunGenerators(CSharpCompilation.Create("Tests")).GetRunResult();
    }

    [Test]
    public async Task Link_to_undefined_entity_reports_located_ORM002()
    {
        var result = Run("module app;\nentity User { pet: Animal; }");

        var diagnostic = result.Diagnostics.SingleOrDefault(d => d.Id == "ORM002");
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Location.GetLineSpan().Path).IsEqualTo("schema/app.dqls");
        await Assert.That(result.Results[0].GeneratedSources.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Unknown_property_type_reports_ORM003()
    {
        var result = Run("module app;\nentity User { name: textt; }");

        await Assert.That(result.Diagnostics.Any(d => d.Id == "ORM003")).IsTrue();
        await Assert.That(result.Results[0].GeneratedSources.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Missing_colon_reports_ORM001()
    {
        var result = Run("module app;\nentity User { id uuid; }");

        await Assert.That(result.Diagnostics.Any(d => d.Id == "ORM001")).IsTrue();
    }
}
