using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 002 (T040): the command pipeline caches on an unchanged rerun (incremental, no Compilation/ISymbol
// leaks) — Constitution VI. Mirrors the schema/query cacheability checks.
public sealed class CommandCacheabilityTests
{
    private const string Schema = "module catalog;\nentity Widget { id: uuid primary; name: str; quantity: int; }";
    private const string Commands = "module catalog;\ncommand CreateWidget(id: uuid, name: str, quantity: int) = insert Widget { id := id, name := name, quantity := quantity };";

    [Test]
    public async Task Command_pipeline_steps_are_cached_on_unchanged_rerun()
    {
        var compilation = CSharpCompilation.Create("Tests");
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/catalog.dqls", Schema),
            new TestAdditionalText("schema/catalog.dql", Commands));

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation.Clone());

        var trackedSteps = driver.GetRunResult().Results[0].TrackedSteps;
        foreach (var stepName in new[] { "LoadCommandFiles", "ParseCommands" })
        {
            var allCached = trackedSteps[stepName]
                .SelectMany(step => step.Outputs)
                .All(output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);

            await Assert.That(allCached).IsTrue();
        }
    }

    [Test]
    public async Task Command_generation_is_deterministic()
    {
        string Run()
        {
            var driver = GeneratorTestHarness.CreateDriver(
                new TestAdditionalText("schema/catalog.dqls", Schema),
                new TestAdditionalText("schema/catalog.dql", Commands));
            driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
            return string.Join(
                "\n",
                driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        }

        await Assert.That(Run()).IsEqualTo(Run());
    }
}
