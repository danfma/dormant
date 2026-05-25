using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dormant.SourceGeneration.Emit;

/// <summary>Maps DormantQL v1 value types to their build-time-known CLR types (spec FR-036).</summary>
internal static class TypeMap
{
    private static readonly Dictionary<string, string> Map = new(System.StringComparer.Ordinal)
    {
        ["str"] = "string",
        ["bool"] = "bool",
        ["int16"] = "short",
        ["int32"] = "int",
        ["int64"] = "long",
        ["int"] = "int",
        ["float32"] = "float",
        ["float64"] = "double",
        ["decimal"] = "decimal",
        ["bigint"] = "global::System.Numerics.BigInteger",
        ["uuid"] = "global::System.Guid",
        ["datetime"] = "global::System.DateTime",
        ["duration"] = "global::System.TimeSpan",
        ["bytes"] = "byte[]",
        ["json"] = "string",
    };

    /// <summary>Attempts to map a DormantQL value type to a CLR type.</summary>
    public static bool TryMap(string dslType, out string clrType) => Map.TryGetValue(dslType, out clrType!);

    // DormantQL value type → PostgreSQL column type, for generated DDL (FR-020). PostgreSQL-specific;
    // a provider-neutral dialect abstraction is a later concern.
    private static readonly Dictionary<string, string> SqlMap = new(System.StringComparer.Ordinal)
    {
        ["str"] = "text",
        ["bool"] = "boolean",
        ["int16"] = "smallint",
        ["int32"] = "integer",
        ["int"] = "integer",
        ["int64"] = "bigint",
        ["float32"] = "real",
        ["float64"] = "double precision",
        ["decimal"] = "numeric",
        ["bigint"] = "numeric",
        ["uuid"] = "uuid",
        ["datetime"] = "timestamptz",
        ["duration"] = "interval",
        ["bytes"] = "bytea",
        ["json"] = "jsonb",
    };

    /// <summary>Maps a DormantQL value type to its PostgreSQL column type (falls back to <c>text</c>).</summary>
    public static string ToSqlType(string dslType) => SqlMap.TryGetValue(dslType, out var sql) ? sql : "text";
}

/// <summary>Deterministic naming helpers (ordinal, culture-invariant) for generated code (research §5).</summary>
internal static class Naming
{
    /// <summary>Converts a DormantQL identifier (e.g. <c>created_at</c>) to PascalCase (<c>CreatedAt</c>).</summary>
    public static string ToPascalCase(string name)
    {
        var sb = new StringBuilder(name.Length);
        var capitalizeNext = true;
        foreach (var c in name)
        {
            if (c is '_' or '-' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Computes the generated namespace as <c>PascalCaseEachPart(rootNamespace + folders + module)</c>
    /// (FR-046), where folders are the schema file's directory relative to the project (e.g.
    /// <c>schema/app.dqls</c> in <c>Dormant.Sample.Quickstart</c> → <c>Dormant.Sample.Quickstart.Schema.App</c>).
    /// Falls back gracefully when the project's root namespace or directory is unknown.
    /// </summary>
    public static string ComputeNamespace(string? rootNamespace, string? projectDir, string filePath, string moduleName)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(rootNamespace))
        {
            parts.AddRange(rootNamespace!.Split('.'));
        }

        var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(projectDir) &&
            dir.StartsWith(projectDir!.TrimEnd('/', '\\'), StringComparison.Ordinal))
        {
            dir = dir.Substring(projectDir!.TrimEnd('/', '\\').Length);
        }

        parts.AddRange(dir.Split('/', '\\'));
        parts.Add(moduleName);

        return string.Join(".", parts.Where(p => p.Length > 0).Select(ToPascalCase));
    }

    /// <summary>Builds a stable, unique hint name for a generated entity source file.</summary>
    public static string HintName(string @namespace, string entityName) =>
        @namespace + "." + entityName + ".g.cs";
}

/// <summary>Minimal indentation-aware writer that always emits <c>\n</c> newlines (determinism).</summary>
internal sealed class SourceWriter
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public SourceWriter Open(string line)
    {
        Line(line);
        Line("{");
        _indent++;
        return this;
    }

    public SourceWriter Close()
    {
        _indent--;
        Line("}");
        return this;
    }

    public SourceWriter Line(string text = "")
    {
        if (text.Length > 0)
        {
            _sb.Append(' ', _indent * 4).Append(text);
        }

        _sb.Append('\n');
        return this;
    }

    public override string ToString() => _sb.ToString();
}
