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
internal sealed record QueryModel(
    string Name,
    string RootEntity,
    string Alias,
    EquatableArray<QueryParameter> Parameters,
    EquatableArray<string> ProjectionFields,
    EquatableArray<FilterCondition> Filters,
    EquatableArray<OrderTerm> OrderBy,
    LimitValue? Limit,
    LimitValue? Offset
)
{
    /// <summary>Whether the result is a flat projection (vs a full entity).</summary>
    public bool IsProjection => ProjectionFields.Count > 0;
}

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

/// <summary>A single filter condition: <c>.column op @param</c> (MVP: own-column comparisons only).</summary>
/// <param name="Column">The entity column on the left.</param>
/// <param name="Op">The comparison operator.</param>
/// <param name="ParameterName">The bound parameter on the right.</param>
internal sealed record FilterCondition(string Column, CompareOp Op, string ParameterName);

/// <summary>An order-by term.</summary>
/// <param name="Column">The entity column to order by.</param>
/// <param name="Descending">Whether the term is descending.</param>
internal sealed record OrderTerm(string Column, bool Descending);

/// <summary>A LIMIT/OFFSET value: either an integer literal or a bound parameter.</summary>
/// <param name="IsParameter">Whether the value is a parameter (vs a literal).</param>
/// <param name="ParameterName">The parameter name when <paramref name="IsParameter"/> is true.</param>
/// <param name="Literal">The integer literal when <paramref name="IsParameter"/> is false.</param>
internal sealed record LimitValue(bool IsParameter, string ParameterName, int Literal);
