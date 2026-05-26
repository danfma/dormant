using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// Foundational coverage for the incremental generator pipeline (T019/T022).
public sealed class GeneratorPipelineTests
{
    [Test]
    public async Task Runs_without_diagnostics_for_a_schema_file()
    {
        var compilation = CSharpCompilation.Create("Tests");
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/app.dqls", "module app;")
        );

        driver = driver.RunGenerators(compilation);

        await Assert.That(driver.GetRunResult().Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Pipeline_is_cacheable_on_unchanged_rerun()
    {
        var compilation = CSharpCompilation.Create("Tests");
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/app.dqls", "module app;")
        );

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation.Clone());

        // Tracking-name string mirrors DormantGenerator.TrackingNames.LoadDslFiles (internal).
        var outputs = driver
            .GetRunResult()
            .Results[0]
            .TrackedSteps["LoadDslFiles"]
            .SelectMany(step => step.Outputs);

        await Assert
            .That(
                outputs.All(o =>
                    o.Reason
                        is IncrementalStepRunReason.Cached
                            or IncrementalStepRunReason.Unchanged
                )
            )
            .IsTrue();
    }
}
