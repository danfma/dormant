using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Dormant.SourceGeneration.Parsing;

namespace Dormant.SourceGeneration.Diagnostics;

/// <summary>Stable diagnostic descriptors for the DormantQL generator/analyzer (spec FR-004/FR-028).</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "DormantQL";

    /// <summary>A schema/query DormantQL file could not be parsed.</summary>
    public static readonly DiagnosticDescriptor SyntaxError = new(
        id: "ORM001",
        title: "DormantQL syntax error",
        messageFormat: "DormantQL syntax error: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A link targets an entity that is not defined in the schema.</summary>
    public static readonly DiagnosticDescriptor UndefinedLinkTarget = new(
        id: "ORM002",
        title: "Link targets an undefined entity",
        messageFormat: "Link '{0}' targets entity '{1}', which is not defined in the schema",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A property declares a value type that is not a known DormantQL v1 type.</summary>
    public static readonly DiagnosticDescriptor UnknownPropertyType = new(
        id: "ORM003",
        title: "Unknown property type",
        messageFormat: "Property '{0}' has unknown DormantQL type '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Use a v1 DormantQL value type such as str, bool, int32, int64, float64, decimal, uuid, datetime, bytes, or json.");
}

/// <summary>An equatable, pipeline-safe description of a diagnostic (research §5).</summary>
/// <param name="Descriptor">The descriptor to report.</param>
/// <param name="Location">The source location, or <see langword="null"/> for a file-less diagnostic.</param>
/// <param name="MessageArgs">Message format arguments.</param>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    /// <summary>Materializes a Roslyn <see cref="Diagnostic"/> (call only in the output stage).</summary>
    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < MessageArgs.Count; i++)
        {
            args[i] = MessageArgs[i];
        }

        return Diagnostic.Create(Descriptor, Location?.ToLocation(), args);
    }
}

/// <summary>An equatable, pipeline-safe source location over a DormantQL file (research §5).</summary>
/// <param name="FilePath">The DormantQL file path.</param>
/// <param name="TextSpan">The character span.</param>
/// <param name="LineSpan">The line/column span (zero-based).</param>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    /// <summary>Materializes a Roslyn <see cref="Location"/>.</summary>
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);
}
