using System.Collections.Generic;
using Dormant.SourceGeneration.Diagnostics;
using Dormant.SourceGeneration.Parsing;

namespace Dormant.SourceGeneration.Schema;

/// <summary>
/// Semantic validation over parsed entities (spec FR-004): every link must target a defined entity
/// (ORM002), reported with the target's source location.
/// </summary>
internal static class SchemaValidator
{
    public static IReadOnlyList<DiagnosticInfo> Validate(IReadOnlyList<EntityModel> entities)
    {
        var defined = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            defined.Add(entity.Name);
        }

        var diagnostics = new List<DiagnosticInfo>();
        foreach (var entity in entities)
        {
            foreach (var link in entity.Links)
            {
                if (!defined.Contains(link.TargetEntity))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.UndefinedLinkTarget,
                        link.TargetLocation,
                        new EquatableArray<string>([link.Name, link.TargetEntity])));
                }
            }
        }

        return diagnostics;
    }
}
