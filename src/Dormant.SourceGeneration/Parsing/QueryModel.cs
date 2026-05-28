using Dormant.SourceGeneration.Diagnostics;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Parsed + validated queries for one DormantQL query file (<c>.dql</c>). Equatable for caching.</summary>
/// <param name="ModuleName">The declared module (shared with the schema; drives the namespace).</param>
/// <param name="FilePath">The query file path (used to derive the generated namespace, FR-046).</param>
/// <param name="Queries">The declared queries, in source order.</param>
/// <param name="Diagnostics">Syntax diagnostics collected while building the model.</param>
internal sealed record QueryFile(
    string ModuleName,
    string FilePath,
    EquatableArray<QueryModel> Queries,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>Whether the file is free of error-severity diagnostics and safe to emit.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>A single declared query (003: <c>from Entity alias [where …] [order by …] select …</c>).</summary>
/// <param name="Name">The unit's authored snake_case name (becomes a PascalCase method via <see cref="Emit.Naming.ToPascalCase"/>).</param>
/// <param name="RootEntity">The entity being selected (the <c>from</c> subject).</param>
/// <param name="Alias">The subject alias declared in <c>from Entity alias</c> (003 — member refs are alias-qualified).</param>
/// <param name="Parameters">Declared parameters, in source order.</param>
/// <param name="ProjectionFields">Projected field names; empty ⇒ full-entity result.</param>
/// <param name="Filters">Conjunctive filter conditions (ANDed; the <c>where … &amp;&amp; …</c> chain).</param>
/// <param name="OrderBy">Order-by terms, in source order.</param>
/// <param name="Limit">Optional LIMIT (literal or parameter). // TODO(003): no surface grammar yet; always null.</param>
/// <param name="Offset">Optional OFFSET (literal or parameter). // TODO(003): no surface grammar yet; always null.</param>
/// <param name="Shape">The 009 root-object select shape, or null for full-entity / flat-projection selects.</param>
/// <param name="Composition">The 009 free-composition select, or null.</param>
/// <param name="IntoType">The 009 US3 <c>into</c> target — a user-owned record type name to materialize into, or null.</param>
internal sealed record QueryModel(
    string Name,
    string RootEntity,
    string Alias,
    EquatableArray<QueryParameter> Parameters,
    EquatableArray<string> ProjectionFields,
    EquatableArray<FilterCondition> Filters,
    EquatableArray<OrderTerm> OrderBy,
    LimitValue? Limit,
    LimitValue? Offset,
    SelectShape? Shape = null,
    FreeComposition? Composition = null,
    string? IntoType = null
)
{
    /// <summary>Whether the result is a flat projection (vs a full entity).</summary>
    public bool IsProjection => ProjectionFields.Count > 0;

    /// <summary>Whether the result is an EdgeQL-style root-object shape (009 US1).</summary>
    public bool IsShaped => Shape is not null;

    /// <summary>Whether the result is a free composition of named members (009 US2).</summary>
    public bool IsComposed => Composition is not null;
}

/// <summary>
/// A free-composition select: <c>select { name = expr, … }</c> — a brand-new result object whose named
/// members are drawn from in-scope sources/navigation (009 US2). This slice supports scalar members
/// (own column or a to-one navigation path); nested-shape members and multi-source <c>with</c> are later.
/// </summary>
/// <param name="Members">The named members, in source order.</param>
internal sealed record FreeComposition(EquatableArray<CompositionMember> Members);

/// <summary>
/// One named member of a <see cref="FreeComposition"/>: <c>name = alias.[ref.]*column</c>.
/// </summary>
/// <param name="Name">The result member name.</param>
/// <param name="Alias">The source alias the path is rooted at.</param>
/// <param name="Path">The member's value path after the alias (to-one refs then a terminal column).</param>
internal sealed record CompositionMember(string Name, string Alias, EquatableArray<string> Path);

/// <summary>
/// A root-object select shape: <c>select alias { node, ref: { … } }</c> (009 US1). The result type is a
/// generated nested immutable record matching the shape.
/// </summary>
/// <param name="RootAlias">The shaped root alias (the <c>a</c> in <c>select a { … }</c>).</param>
/// <param name="Nodes">The shape members, in source order.</param>
internal sealed record SelectShape(string RootAlias, EquatableArray<ShapeNode> Nodes);

/// <summary>
/// One member of a shape: a scalar field (<see cref="IsRef"/> false) or a nested to-one reference shape
/// (<see cref="IsRef"/> true, with its own <see cref="Children"/>). To-many is a later slice.
/// </summary>
/// <param name="Name">The field or reference name (bare, resolved against the node's entity).</param>
/// <param name="IsRef">Whether this is a nested reference shape.</param>
/// <param name="Children">The nested shape members (for a reference node).</param>
internal sealed record ShapeNode(string Name, bool IsRef, EquatableArray<ShapeNode> Children);

/// <summary>A declared query parameter.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="DslType">The DormantQL type as written.</param>
/// <param name="ClrType">The mapped CLR type, or empty when unknown.</param>
/// <param name="IsOptional">Whether declared <c>optional</c> (FR-012/FR-031): a filter on it is a conditional fragment.</param>
internal sealed record QueryParameter(
    string Name,
    string DslType,
    string ClrType,
    bool IsOptional = false
);

/// <summary>A comparison operator usable in a filter condition (FR-032, MVP subset).</summary>
internal enum CompareOp
{
    /// <summary><c>==</c> (renders SQL <c>=</c>)</summary>
    Eq,

    /// <summary><c>!=</c> (renders SQL <c>&lt;&gt;</c>)</summary>
    Neq,

    /// <summary><c>&lt;</c></summary>
    Lt,

    /// <summary><c>&gt;</c></summary>
    Gt,

    /// <summary><c>&lt;=</c></summary>
    Le,

    /// <summary><c>&gt;=</c></summary>
    Ge,

    /// <summary><c>like</c></summary>
    Like,

    /// <summary><c>ilike</c></summary>
    ILike,
}

/// <summary>
/// A single filter condition: <c>alias.[ref.]*column op @param</c>. <paramref name="Column"/> is the
/// terminal column; <paramref name="NavRefs"/> is the chain of to-one references navigated to reach it
/// (empty ⇒ own column on the root; non-empty ⇒ the query joins those references — 009 P-B).
/// </summary>
/// <param name="Column">The terminal column on the left.</param>
/// <param name="Op">The comparison operator.</param>
/// <param name="ParameterName">The bound parameter on the right.</param>
/// <param name="NavRefs">To-one reference names navigated before the terminal column (empty for own-column).</param>
internal sealed record FilterCondition(
    string Column,
    CompareOp Op,
    string ParameterName,
    EquatableArray<string> NavRefs = default
);

/// <summary>An order-by term.</summary>
/// <param name="Column">The entity column to order by.</param>
/// <param name="Descending">Whether the term is descending.</param>
internal sealed record OrderTerm(string Column, bool Descending);

/// <summary>A LIMIT/OFFSET value: either an integer literal or a bound parameter.</summary>
/// <param name="IsParameter">Whether the value is a parameter (vs a literal).</param>
/// <param name="ParameterName">The parameter name when <paramref name="IsParameter"/> is true.</param>
/// <param name="Literal">The integer literal when <paramref name="IsParameter"/> is false.</param>
internal sealed record LimitValue(bool IsParameter, string ParameterName, int Literal);
