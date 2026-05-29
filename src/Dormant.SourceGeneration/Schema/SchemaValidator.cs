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
                    diagnostics.Add(
                        new DiagnosticInfo(
                            DiagnosticDescriptors.UndefinedLinkTarget,
                            reference.TargetLocation,
                            new EquatableArray<string>([reference.Name, reference.TargetEntity])
                        )
                    );
                }
            }

            ValidateConstraints(entity, diagnostics);
        }

        return diagnostics;
    }

    // Feature 012: constraint/annotation semantic checks (ORM030/ORM031/ORM036).
    private static void ValidateConstraints(EntityModel entity, List<DiagnosticInfo> diagnostics)
    {
        var members = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var p in entity.Properties)
        {
            members.Add(p.Name);
        }

        // Member-level: type compatibility (ORM030) + member-check member refs (ORM031) + annotations.
        foreach (var p in entity.Properties)
        {
            foreach (var c in p.Constraints)
            {
                if (!AppliesTo(c.Kind, p.DslType))
                {
                    diagnostics.Add(
                        new DiagnosticInfo(
                            DiagnosticDescriptors.ConstraintTypeMismatch,
                            c.Location,
                            new EquatableArray<string>([KindName(c.Kind), p.Name, p.DslType])
                        )
                    );
                }

                CheckMemberRefs(c, entity.Name, members, diagnostics);
            }

            foreach (var a in p.Annotations)
            {
                ValidateAnnotation(a, diagnostics);
            }
        }

        // Entity-level: `on (…)` targets + check refs (ORM031) + annotations.
        foreach (var c in entity.Constraints)
        {
            foreach (var target in c.Targets)
            {
                if (!members.Contains(target))
                {
                    diagnostics.Add(
                        new DiagnosticInfo(
                            DiagnosticDescriptors.ConstraintUnknownMember,
                            c.Location,
                            new EquatableArray<string>([target, entity.Name])
                        )
                    );
                }
            }

            CheckMemberRefs(c, entity.Name, members, diagnostics);
        }

        foreach (var a in entity.Annotations)
        {
            ValidateAnnotation(a, diagnostics);
        }
    }

    private static void CheckMemberRefs(
        ConstraintModel c,
        string entityName,
        HashSet<string> members,
        List<DiagnosticInfo> diagnostics
    )
    {
        foreach (var t in c.CheckTokens)
        {
            if (t.Kind == CheckTokenKind.Identifier && !members.Contains(t.Text))
            {
                diagnostics.Add(
                    new DiagnosticInfo(
                        DiagnosticDescriptors.ConstraintUnknownMember,
                        c.Location,
                        new EquatableArray<string>([t.Text, entityName])
                    )
                );
            }
        }
    }

    private static void ValidateAnnotation(AnnotationModel a, List<DiagnosticInfo> diagnostics)
    {
        // v1 minimum annotation set: only `column("…")`.
        if (a.Name != "column")
        {
            diagnostics.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.InvalidAnnotationOrTarget,
                    a.Location,
                    new EquatableArray<string>([$"unknown annotation '{a.Name}'"])
                )
            );
            return;
        }

        if (a.Args.Count != 1 || !a.Args[0].IsString)
        {
            diagnostics.Add(
                new DiagnosticInfo(
                    DiagnosticDescriptors.InvalidAnnotationOrTarget,
                    a.Location,
                    new EquatableArray<string>([
                        "annotation 'column' takes a single string argument, e.g. column(\"name\")",
                    ])
                )
            );
        }
    }

    // Type applicability of a constraint kind to a member's DormantQL value type.
    private static bool AppliesTo(ConstraintKind kind, string dslType) =>
        kind switch
        {
            ConstraintKind.MaxLength
            or ConstraintKind.MinLength
            or ConstraintKind.Length
            or ConstraintKind.Regex => IsStringType(dslType),
            ConstraintKind.Max
            or ConstraintKind.Min
            or ConstraintKind.MaxExclusive
            or ConstraintKind.MinExclusive
            or ConstraintKind.Range => IsOrderedType(dslType),
            // unique/primary/concurrency/check/one_of apply to any scalar member.
            _ => true,
        };

    private static bool IsStringType(string dsl) => dsl is "str" or "string";

    private static bool IsOrderedType(string dsl) =>
        dsl
            is "int"
                or "int16"
                or "int32"
                or "int64"
                or "long"
                or "double"
                or "float32"
                or "float64"
                or "decimal"
                or "bigint"
                or "datetime"
                or "date"
                or "duration";

    private static string KindName(ConstraintKind kind) =>
        kind switch
        {
            ConstraintKind.Unique => "unique",
            ConstraintKind.Check => "check",
            ConstraintKind.OneOf => "one_of",
            ConstraintKind.Max => "max",
            ConstraintKind.Min => "min",
            ConstraintKind.MaxExclusive => "max_exclusive",
            ConstraintKind.MinExclusive => "min_exclusive",
            ConstraintKind.MaxLength => "max_length",
            ConstraintKind.MinLength => "min_length",
            ConstraintKind.Length => "length",
            ConstraintKind.Range => "range",
            ConstraintKind.Regex => "regex",
            ConstraintKind.Primary => "primary",
            ConstraintKind.Concurrency => "concurrency",
            _ => kind.ToString(),
        };
}
