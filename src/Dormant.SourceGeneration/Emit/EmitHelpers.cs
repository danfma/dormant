using System.Collections.Generic;
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
            if (c == '_')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    /// <summary>Builds a stable, unique hint name for a generated entity source file.</summary>
    public static string HintName(string moduleName, string entityName) =>
        moduleName + "_" + entityName + ".g.cs";
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
