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
        // 003 lowercase type keywords (data-model.md vocabulary).
        ["string"] = "string",
        ["long"] = "long",
        ["double"] = "double",
        ["date"] = "global::System.DateOnly",
        // Carried 002 type keywords (the .dqls schema grammar is unchanged, so these still parse).
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

    // DormantQL value type → SQL column type is now per-dialect (005 D6); each
    // Ir.Dialects.ISqlDialectRenderer.TypeName owns its mapping. The DDL IR carries the DormantQL type.
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

    /// <summary>
    /// Emits <paramref name="content"/> (one line of SQL) as a C# multi-line raw string literal — an opening
    /// fence line, the content line, and a closing fence line followed by <paramref name="trailing"/> — all
    /// sharing <paramref name="indent"/> so the raw-string dedent yields the content verbatim (004 FR-001).
    /// A single-line raw string can't end with <c>"</c> (our SQL ends with a quoted identifier), so the
    /// multi-line form is used. The fence length adapts to the content's longest quote run (FR-003); the
    /// literal is non-interpolated, so <c>$n</c> placeholders and braces are preserved verbatim (FR-004).
    /// </summary>
    public SourceWriter RawArg(string indent, string content, string trailing)
    {
        var fence = new string('"', RawFence(content));
        Line(indent + fence);
        Line(indent + content);
        Line(indent + fence + trailing);
        return this;
    }

    // The fence must be longer than the longest run of '"' in the content; never shorter than 3.
    private static int RawFence(string content)
    {
        var max = 0;
        var current = 0;
        foreach (var c in content)
        {
            if (c == '"')
            {
                current++;
                if (current > max)
                {
                    max = current;
                }
            }
            else
            {
                current = 0;
            }
        }

        return System.Math.Max(3, max + 1);
    }

    public override string ToString() => _sb.ToString();
}
