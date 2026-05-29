using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US1 (T024): the schema pipeline caches on an unchanged rerun (no Compilation/ISymbol leaks).
public sealed class SchemaCacheabilityTests
{
    [Test]
    public async Task Schema_pipeline_steps_are_cached_on_unchanged_rerun()
    {
        const string schema =
            "module app;\nentity User { id: Uuid { constraint primary; } email: String; }";
        var compilation = CSharpCompilation.Create("Tests");
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/app.dqls", schema)
        );

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation.Clone());

        var trackedSteps = driver.GetRunResult().Results[0].TrackedSteps;
        foreach (var stepName in new[] { "LoadDslFiles", "ParseSchema" })
        {
            var allCached = trackedSteps[stepName]
                .SelectMany(step => step.Outputs)
                .All(output =>
                    output.Reason
                        is IncrementalStepRunReason.Cached
                            or IncrementalStepRunReason.Unchanged
                );

            await Assert.That(allCached).IsTrue();
        }
    }
}
