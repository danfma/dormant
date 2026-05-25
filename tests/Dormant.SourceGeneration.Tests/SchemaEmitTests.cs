using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.SourceGeneration.Tests;

// US1 (T023): schema -> generated partial entity types with properties and links.
public sealed class SchemaEmitTests
{
    private const string TwoEntities = """
        module app;

        entity User {
          id: uuid primary;
          email: str;
          multi posts -> Post;
          version: int concurrency;
        }

        entity Post {
          id: uuid primary;
          title: str;
          single author -> User;
        }
        """;

    private static string RunAndConcat(out int sourceCount)
    {
        var driver = GeneratorTestHarness.CreateDriver(new TestAdditionalText("schema/app.dqls", TwoEntities));
        driver = driver.RunGenerators(CSharpCompilation.Create("Tests"));
        var sources = driver.GetRunResult().Results[0].GeneratedSources;
        sourceCount = sources.Length;
        return string.Join("\n", sources.Select(s => s.SourceText.ToString()));
    }

    [Test]
    public async Task Emits_one_partial_per_entity_with_members()
    {
        var generated = RunAndConcat(out var count);

        await Assert.That(count).IsEqualTo(2);
        await Assert.That(generated).Contains("public partial class User");
        await Assert.That(generated).Contains("public partial class Post");
        await Assert.That(generated).Contains("global::System.Guid Id { get; set; }");
        await Assert.That(generated).Contains("public string Email { get; set; } = default!;");
        await Assert.That(generated).Contains("global::Dormant.Abstractions.Links.LinkSet<Post> Posts");
        await Assert.That(generated).Contains("global::Dormant.Abstractions.Links.Link<User> Author");
    }

    [Test]
    public async Task Generation_is_deterministic()
    {
        var first = RunAndConcat(out _);
        var second = RunAndConcat(out _);

        await Assert.That(first).IsEqualTo(second);
    }
}
