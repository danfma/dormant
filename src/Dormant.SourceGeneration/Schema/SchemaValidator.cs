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
            foreach (var reference in entity.References)
            {
                if (!defined.Contains(reference.TargetEntity))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.UndefinedLinkTarget,
                        reference.TargetLocation,
                        new EquatableArray<string>([reference.Name, reference.TargetEntity])));
                }
            }
        }

        return diagnostics;
    }
}
