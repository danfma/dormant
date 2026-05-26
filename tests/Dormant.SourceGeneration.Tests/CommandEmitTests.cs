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

        mutation create_widget(id: uuid, name: string, quantity: int) {
          insert Widget w {
            w.id = id
            w.name = name
            w.quantity = quantity
          }
        }
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
            .Contains("INSERT INTO \"catalog\".\"widget\" (\"id\", \"name\", \"quantity\") VALUES ($1, $2, $3) RETURNING \"id\", \"name\", \"quantity\"");
        await Assert.That(generated).Contains("new global::Dormant.Abstractions.Querying.CompiledCommand<Widget>(statement, static reader => new Widget(reader))");
        await Assert.That(generated).Contains("session.ExecuteCommandAsync(command, cancellationToken)");
    }

    private static string RunWith(string commands)
    {
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/catalog.dqls", Schema),
            new TestAdditionalText("schema/catalog.dql", commands));
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        return string.Join(
            "\n",
            driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
    }

    // 003 T015/FR-017: `returning alias` shapes the result as the full entity (the default shape, made explicit).
    [Test]
    public async Task Insert_returning_entity_emits_entity_result()
    {
        var generated = RunWith(
            "module catalog;\nmutation create_widget(id: uuid, name: string, quantity: int) { insert Widget w { w.id = id w.name = name w.quantity = quantity } returning w }");

        await Assert.That(generated).Contains("global::System.Threading.Tasks.ValueTask<Widget> CreateWidget(");
        await Assert.That(generated).Contains("RETURNING \"id\", \"name\", \"quantity\"");
    }

    // 003 T015/FR-017: `returning alias.member` shapes a scalar result (RETURNING one column, read column 0).
    [Test]
    public async Task Insert_returning_scalar_emits_scalar_result()
    {
        var generated = RunWith(
            "module catalog;\nmutation create_widget(id: uuid, name: string, quantity: int) { insert Widget w { w.id = id w.name = name w.quantity = quantity } returning w.id }");

        await Assert.That(generated).Contains("global::System.Threading.Tasks.ValueTask<global::System.Guid> CreateWidget(");
        await Assert.That(generated).Contains("VALUES ($1, $2, $3) RETURNING \"id\"");
        await Assert.That(generated).Contains("reader.GetValue<global::System.Guid>(0)");
    }

    // 003 T015/FR-017: `returning { … }` shapes a distinct projection record exposing exactly those members.
    [Test]
    public async Task Insert_returning_projection_emits_distinct_record()
    {
        var generated = RunWith(
            "module catalog;\nmutation create_widget(id: uuid, name: string, quantity: int) { insert Widget w { w.id = id w.name = name w.quantity = quantity } returning { w.id, w.name } }");

        await Assert.That(generated).Contains("public sealed record CreateWidgetResult(global::System.Guid Id, string Name);");
        await Assert.That(generated).Contains("global::System.Threading.Tasks.ValueTask<CreateWidgetResult> CreateWidget(");
        await Assert.That(generated).Contains("RETURNING \"id\", \"name\"");
    }

    // 003 T015/FR-017: an `update … returning alias` RETURNs the entity (UPDATE … RETURNING), not a count.
    [Test]
    public async Task Update_returning_entity_emits_update_returning()
    {
        var generated = RunWith(
            "module catalog;\nmutation bump_widget(id: uuid, quantity: int) { update Widget w where w.id == id set { w.quantity = quantity } returning w }");

        await Assert.That(generated).Contains("global::System.Threading.Tasks.ValueTask<Widget> BumpWidget(");
        await Assert.That(generated).Contains("UPDATE \"catalog\".\"widget\" SET \"quantity\" = $1 WHERE \"id\" = $2 RETURNING \"id\", \"name\", \"quantity\"");
    }

    // 003 T015/FR-017: a `delete … returning alias.member` RETURNs a scalar (DELETE … RETURNING one column).
    [Test]
    public async Task Delete_returning_scalar_emits_delete_returning()
    {
        var generated = RunWith(
            "module catalog;\nmutation drop_widget(id: uuid) { delete Widget w where w.id == id returning w.id }");

        await Assert.That(generated).Contains("global::System.Threading.Tasks.ValueTask<global::System.Guid> DropWidget(");
        await Assert.That(generated).Contains("DELETE FROM \"catalog\".\"widget\" WHERE \"id\" = $1 RETURNING \"id\"");
        await Assert.That(generated).Contains("session.ExecuteCommandAsync(command, cancellationToken)");
    }
}
