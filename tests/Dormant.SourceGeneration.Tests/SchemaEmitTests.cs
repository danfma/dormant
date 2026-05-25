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
          manager: User?;
          posts: Set<Post>;
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

        // Two sources per entity: the partial class + its materializer.
        await Assert.That(count).IsEqualTo(4);
        await Assert.That(generated).Contains("public partial class User");
        await Assert.That(generated).Contains("public partial class Post");
        await Assert.That(generated).Contains("public required global::System.Guid Id { get; set; }");
        await Assert.That(generated).Contains("public required string Email { get; set; }");
        await Assert.That(generated).Contains("public string? Bio { get; set; }");
        // Optional single ref → Ref<User?> with Unloaded initializer (FR-047/048/049).
        await Assert.That(generated).Contains("global::Dormant.Abstractions.Entities.Ref<User?> Manager { get; set; } = global::Dormant.Abstractions.Entities.Ref<User?>.Unloaded;");
        // Collection → RefSet with Unloaded initializer, never = [] (FR-049).
        await Assert.That(generated).Contains("global::Dormant.Abstractions.Entities.RefSet<Post> Posts { get; set; } = global::Dormant.Abstractions.Entities.RefSet<Post>.Unloaded;");
        // Required single ref → required Ref<User> (FR-047/048).
        await Assert.That(generated).Contains("public required global::Dormant.Abstractions.Entities.Ref<User> Author { get; set; }");
        // PK identity equality emitted (FR-051).
        await Assert.That(generated).Contains("public bool Equals(User? other)");
        await Assert.That(generated).Contains(": global::System.IEquatable<User>");
        // No-reflection materializer (FR-017/048, T044): UnsafeAccessor ctor (past required) + field accessors.
        await Assert.That(generated).Contains("internal static class UserMaterialization");
        await Assert.That(generated).Contains("global::System.Runtime.CompilerServices.UnsafeAccessorKind.Constructor");
        await Assert.That(generated).Contains("public static User Materialize(global::Dormant.Abstractions.Querying.IFieldReader reader)");
    }

    [Test]
    public async Task Generation_is_deterministic()
    {
        var first = RunAndConcat(out _);
        var second = RunAndConcat(out _);

        await Assert.That(first).IsEqualTo(second);
    }
}
