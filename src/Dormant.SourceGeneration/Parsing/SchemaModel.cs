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
/// <param name="References">Relationship references, in source order.</param>
/// <param name="NameOverride">Explicit database table name (<c>db("…")</c>); overrides the convention (FR-054).</param>
internal sealed record EntityModel(
    string Name,
    EquatableArray<PropertyModel> Properties,
    EquatableArray<ReferenceModel> References,
    string? NameOverride = null);

/// <summary>A declared value property.</summary>
/// <param name="Name">The DormantQL property name.</param>
/// <param name="DslType">The DormantQL value type as written (e.g. <c>str</c>).</param>
/// <param name="ClrType">The mapped CLR type (e.g. <c>string</c>), or empty when the type is unknown.</param>
/// <param name="IsNullable">Whether the property is nullable (declared with a trailing <c>?</c>).</param>
/// <param name="IsPrimary">Whether the property is part of the primary key.</param>
/// <param name="IsConcurrency">Whether the property is the optimistic-concurrency token.</param>
/// <param name="NameOverride">Explicit database column name (<c>db("…")</c>); overrides the convention (FR-054).</param>
internal sealed record PropertyModel(
    string Name,
    string DslType,
    string ClrType,
    bool IsNullable,
    bool IsPrimary,
    bool IsConcurrency,
    string? NameOverride = null);

/// <summary>The kind of relationship reference (FR-049): single, or an NHibernate collection.</summary>
internal enum ReferenceKind
{
    /// <summary>Single reference → <c>Ref&lt;T&gt;</c>.</summary>
    Ref,

    /// <summary>Unordered, unique → <c>RefSet&lt;T&gt;</c>.</summary>
    Set,

    /// <summary>Ordered → <c>RefList&lt;T&gt;</c>.</summary>
    List,

    /// <summary>Unordered, duplicates allowed → <c>RefBag&lt;T&gt;</c>.</summary>
    Bag,

    /// <summary>Keyed → <c>RefMap&lt;TKey,TValue&gt;</c>.</summary>
    Map,
}

/// <summary>
/// A declared relationship reference (syntax: <c>name: Target[?]</c> | <c>name: Set/List/Bag/Map&lt;…&gt;</c>,
/// FR-047/FR-049).
/// </summary>
/// <param name="Name">The DormantQL reference name.</param>
/// <param name="TargetEntity">The target entity name.</param>
/// <param name="Kind">The reference kind (single or a collection).</param>
/// <param name="KeyType">For <see cref="ReferenceKind.Map"/>, the DSL key type; otherwise <see langword="null"/>.</param>
/// <param name="IsRequired">For a single ref: required (bare) vs optional (<c>Target?</c>). Collections are optional.</param>
/// <param name="TargetLocation">Source location of the target entity name (for located diagnostics).</param>
internal sealed record ReferenceModel(
    string Name,
    string TargetEntity,
    ReferenceKind Kind,
    string? KeyType,
    bool IsRequired,
    LocationInfo TargetLocation);
