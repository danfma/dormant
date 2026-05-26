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
        var driver = GeneratorTestHarness.CreateDriver(
            new TestAdditionalText("schema/app.dqls", TwoEntities)
        );
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
        await Assert
            .That(generated)
            .Contains("public required global::System.Guid Id { get; init; }");
        await Assert.That(generated).Contains("public required string Email { get; init; }");
        await Assert.That(generated).Contains("public string? Bio { get; init; }");
        // Optional single ref → Ref<User?> with Unloaded initializer (FR-047/048/049).
        await Assert
            .That(generated)
            .Contains(
                "global::Dormant.Abstractions.Entities.Ref<User?> Manager { get; init; } = global::Dormant.Abstractions.Entities.Ref<User?>.Unloaded;"
            );
        // Collection → RefSet with Unloaded initializer, never = [] (FR-049).
        await Assert
            .That(generated)
            .Contains(
                "global::Dormant.Abstractions.Entities.RefSet<Post> Posts { get; init; } = global::Dormant.Abstractions.Entities.RefSet<Post>.Unloaded;"
            );
        // Required single ref → required Ref<User> (FR-047/048).
        await Assert
            .That(generated)
            .Contains(
                "public required global::Dormant.Abstractions.Entities.Ref<User> Author { get; init; }"
            );
        // PK identity equality emitted (FR-051).
        await Assert.That(generated).Contains("public bool Equals(User? other)");
        await Assert.That(generated).Contains(": global::System.IEquatable<User>");
        // Materialization (FR-048): a [SetsRequiredMembers] ctor on the entity partial reading value
        // columns via ordinary setters — no UnsafeAccessor, no backing-field access.
        await Assert
            .That(generated)
            .Contains("[global::System.Diagnostics.CodeAnalysis.SetsRequiredMembers]");
        await Assert
            .That(generated)
            .Contains("internal User(global::Dormant.Abstractions.Querying.IFieldReader reader)");
        await Assert.That(generated).Contains("public User() { }");
        await Assert.That(generated).DoesNotContain("UnsafeAccessor");
        // 002: entities are IMMUTABLE — init-only, no public setters.
        await Assert.That(generated).Contains("public required string Email { get; init; }");
        await Assert.That(generated).DoesNotContain("get; set;");
        // Per-entity binding (002): read + schema metadata only — Materialize, SelectByKey, Schema,
        // CreateTableSql. No INSERT/UPDATE/DELETE/Snapshot/change-tracking (writes are commands).
        await Assert
            .That(generated)
            .Contains(
                "internal sealed class UserBinding : global::Dormant.Abstractions.Entities.IEntityBinding<User>"
            );
        await Assert
            .That(generated)
            .Contains("global::System.Runtime.CompilerServices.ModuleInitializer");
        await Assert
            .That(generated)
            .Contains(
                "public User Materialize(global::Dormant.Abstractions.Querying.IFieldReader reader)"
            );
        await Assert.That(generated).Contains("return new User(reader);");
        await Assert.That(generated).Contains("public string CreateTableSql =>");
        await Assert
            .That(generated)
            .Contains("CREATE TABLE IF NOT EXISTS \\\"app\\\".\\\"user\\\"");
        await Assert.That(generated).DoesNotContain("INSERT INTO");
        await Assert.That(generated).DoesNotContain("TracksConcurrency");
        await Assert.That(generated).DoesNotContain("Snapshot");
        // FR-020: each single reference adds a `<ref>_id` foreign-key column to CREATE TABLE (typed as the
        // target's PK); collections do NOT get a FK column.
        await Assert.That(generated).Contains("\\\"author_id\\\""); // Post.author (required single ref)
        await Assert.That(generated).Contains("\\\"manager_id\\\""); // User.manager (optional single ref)
        await Assert.That(generated).DoesNotContain("\\\"posts_id\\\""); // Set<Post> collection → no FK column
    }

    [Test]
    public async Task Generation_is_deterministic()
    {
        var first = RunAndConcat(out _);
        var second = RunAndConcat(out _);

        await Assert.That(first).IsEqualTo(second);
    }
}
