using System.Collections.Generic;
using System.Text;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Parsing;

namespace Dormant.SourceGeneration.Emit;

/// <summary>The project-level database naming convention (spec FR-052/FR-053).</summary>
internal enum NamingConvention
{
    /// <summary>Default: map identifiers to <c>snake_case</c> (e.g. <c>RecentPost</c> → <c>recent_post</c>).</summary>
    SnakeCase,

    /// <summary>Use the authored identifier verbatim.</summary>
    Verbatim,
}

/// <summary>
/// Resolves a schema identifier (entity/property name) to its database identifier under the active
/// <see cref="NamingConvention"/> (FR-052/FR-056). Pure, deterministic, build-time string work — no
/// runtime cost. An explicit per-unit override always wins (FR-054).
/// </summary>
internal static class NamingConventions
{
    /// <summary>Parses the project build-property value into a convention (default <see cref="NamingConvention.SnakeCase"/>).</summary>
    public static NamingConvention Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "verbatim" => NamingConvention.Verbatim,
        _ => NamingConvention.SnakeCase,
    };

    /// <summary>Resolves a database name: the explicit <paramref name="nameOverride"/> if present, else the convention.</summary>
    public static string Resolve(string identifier, string? nameOverride, NamingConvention convention)
    {
        if (!string.IsNullOrEmpty(nameOverride))
        {
            return nameOverride!;
        }

        return convention switch
        {
            NamingConvention.Verbatim => identifier,
            _ => ToSnakeCase(identifier),
        };
    }

    // Inserts '_' before each interior uppercase letter and lowercases everything; idempotent on
    // already-snake identifiers (no doubled underscores). RecentPost → recent_post, createdAt →
    // created_at, created_at → created_at, id → id.
    private static string ToSnakeCase(string identifier)
    {
        if (identifier.Length == 0)
        {
            return identifier;
        }

        var sb = new StringBuilder(identifier.Length + 4);
        for (var i = 0; i < identifier.Length; i++)
        {
            var c = identifier[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && identifier[i - 1] != '_')
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

/// <summary>Build-time checks over resolved database names (FR-057).</summary>
internal static class NameResolution
{
    /// <summary>
    /// Finds value-column name collisions within an entity under the active convention: two distinct
    /// members resolving to the same database column name (FR-057). Reported as ORM013.
    /// </summary>
    public static IEnumerable<DiagnosticInfo> FindColumnCollisions(EntityModel entity, NamingConvention convention)
    {
        var seen = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var property in entity.Properties)
        {
            var dbName = NamingConventions.Resolve(property.Name, property.NameOverride, convention);
            if (seen.TryGetValue(dbName, out var firstMember))
            {
                yield return new DiagnosticInfo(
                    DiagnosticDescriptors.NameCollision,
                    null,
                    new EquatableArray<string>([entity.Name, firstMember, property.Name, dbName]));
            }
            else
            {
                seen[dbName] = property.Name;
            }
        }
    }
}
