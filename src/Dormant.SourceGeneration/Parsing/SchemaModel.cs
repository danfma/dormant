using Dormant.SourceGeneration.Diagnostics;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Parsed + validated schema for one DormantQL file. Equatable for pipeline caching.</summary>
/// <param name="ModuleName">The declared module (maps to the DB schema; part of the namespace).</param>
/// <param name="FilePath">The schema file path (used to derive the generated namespace, FR-046).</param>
/// <param name="Entities">The declared entities, in source order.</param>
/// <param name="Diagnostics">Syntax/validation diagnostics collected while building the model.</param>
internal sealed record SchemaModel(
    string ModuleName,
    string FilePath,
    EquatableArray<EntityModel> Entities,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>Whether the schema is free of error-severity diagnostics and safe to emit.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>A declared entity.</summary>
/// <param name="Name">The entity (type) name.</param>
/// <param name="Properties">Scalar/value properties, in source order.</param>
/// <param name="Links">Relationship links, in source order.</param>
internal sealed record EntityModel(
    string Name,
    EquatableArray<PropertyModel> Properties,
    EquatableArray<LinkModel> Links);

/// <summary>A declared value property.</summary>
/// <param name="Name">The DormantQL property name.</param>
/// <param name="DslType">The DormantQL value type as written (e.g. <c>str</c>).</param>
/// <param name="ClrType">The mapped CLR type (e.g. <c>string</c>), or empty when the type is unknown.</param>
/// <param name="IsNullable">Whether the property is nullable (declared with a trailing <c>?</c>).</param>
/// <param name="IsPrimary">Whether the property is part of the primary key.</param>
/// <param name="IsConcurrency">Whether the property is the optimistic-concurrency token.</param>
internal sealed record PropertyModel(
    string Name,
    string DslType,
    string ClrType,
    bool IsNullable,
    bool IsPrimary,
    bool IsConcurrency);

/// <summary>A declared relationship link (syntax: <c>name: [multi] Target[?]</c>, FR-047).</summary>
/// <param name="Name">The DormantQL link name.</param>
/// <param name="TargetEntity">The target entity name.</param>
/// <param name="IsMulti">Whether the link is multi-valued (<c>multi</c>) vs single.</param>
/// <param name="IsRequired">Whether a single link is required (bare) vs optional (<c>Target?</c>). Ignored for multi.</param>
/// <param name="TargetLocation">Source location of the target entity name (for located diagnostics).</param>
internal sealed record LinkModel(
    string Name,
    string TargetEntity,
    bool IsMulti,
    bool IsRequired,
    LocationInfo TargetLocation);
