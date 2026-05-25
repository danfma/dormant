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
          bio: str?;
          posts: multi Post;
          version: int concurrency;
        }

        entity Post {
          id: uuid primary;
          title: str;
          author: User;
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
        await Assert.That(generated).Contains("public required global::System.Guid Id { get; set; }");
        await Assert.That(generated).Contains("public required string Email { get; set; }");
        await Assert.That(generated).Contains("public string? Bio { get; set; }");
        await Assert.That(generated).Contains("public global::Dormant.Abstractions.Links.LinkSet<Post> Posts { get; set; }");
        await Assert.That(generated).Contains("public required global::Dormant.Abstractions.Links.Link<User> Author { get; set; }");
        // Generated into the .NET-friendly namespace (no MSBuild config in tests → folders + module).
        await Assert.That(generated).Contains("namespace");
    }

    [Test]
    public async Task Generation_is_deterministic()
    {
        var first = RunAndConcat(out _);
        var second = RunAndConcat(out _);

        await Assert.That(first).IsEqualTo(second);
    }
}
