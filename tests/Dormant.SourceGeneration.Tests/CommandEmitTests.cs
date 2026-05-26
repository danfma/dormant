using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// 002 US1 (T011): an authored `insert` command compiles to a `{Module}Commands` ISession extension method
// (C# 14 extension block) carrying build-time INSERT … RETURNING SQL on a CompiledCommand<T>.
public sealed class CommandEmitTests
{
    private const string Schema = """
        module catalog;

        entity Widget {
          id: uuid primary;
          name: str;
          quantity: int;
        }
        """;

    private const string Commands = """
        module catalog;

        command CreateWidget(id: uuid, name: str, quantity: int) =
          insert Widget { id := id, name := name, quantity := quantity };
        """;

    private static string Run()
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/catalog.dqls", Schema),
            new TestAdditionalText("schema/catalog.dql", Commands));
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        return string.Join(
            "\n",
            driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Insert_command_emits_session_extension_with_returning_sql()
    {
        var generated = Run();

        await Assert.That(generated).Contains("public static partial class CatalogCommands");
        await Assert.That(generated).Contains("extension(global::Dormant.Abstractions.Sessions.ISession session)");
        await Assert.That(generated)
            .Contains("global::System.Threading.Tasks.ValueTask<Widget> CreateWidget(global::System.Guid id, string name, int quantity,");
        // Build-time, schema-qualified INSERT … RETURNING.
        await Assert.That(generated)
            .Contains("INSERT INTO \\\"catalog\\\".\\\"widget\\\" (\\\"id\\\", \\\"name\\\", \\\"quantity\\\") VALUES ($1, $2, $3) RETURNING \\\"id\\\", \\\"name\\\", \\\"quantity\\\"");
        await Assert.That(generated).Contains("new global::Dormant.Abstractions.Querying.CompiledCommand<Widget>(statement, static reader => new Widget(reader))");
        await Assert.That(generated).Contains("session.ExecuteCommandAsync(command, cancellationToken)");
    }
}
