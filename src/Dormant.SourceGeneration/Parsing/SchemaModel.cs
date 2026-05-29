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
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>Whether the schema is free of error-severity diagnostics and safe to emit.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>The standard-library kind of a constraint (Feature 012, EdgeQL-inspired).</summary>
internal enum ConstraintKind
{
    Primary,
    Concurrency,
    Unique,
    Check,
    OneOf,
    Max,
    Min,
    MaxExclusive,
    MinExclusive,
    MaxLength,
    MinLength,
    Length,
    Range,
    Regex,
}

/// <summary>One argument of a constraint/annotation call: positional (<c>Name</c> null) or named.</summary>
/// <param name="Name">Argument name for a named arg (<c>min = 1</c>); <see langword="null"/> when positional.</param>
/// <param name="Value">The literal value as written (string content without quotes, or number/bool text).</param>
/// <param name="IsString">Whether the value came from a string literal (so SQL rendering quotes it).</param>
internal sealed record ConstraintArg(string? Name, string Value, bool IsString = false);

/// <summary>The lexical role of a token captured from a <c>check (…)</c> expression.</summary>
internal enum CheckTokenKind
{
    /// <summary>A member/identifier reference (resolved to a quoted column at emit time).</summary>
    Identifier,

    /// <summary>An operator, already mapped to its SQL spelling (e.g. <c>==</c> → <c>=</c>).</summary>
    Operator,

    /// <summary>A numeric literal (emitted verbatim).</summary>
    Number,

    /// <summary>A string literal (emitted single-quoted, with quotes escaped).</summary>
    String,

    /// <summary><c>(</c>.</summary>
    LParen,

    /// <summary><c>)</c>.</summary>
    RParen,
}

/// <summary>One token of a captured <c>check (…)</c> expression (Feature 012).</summary>
/// <param name="Kind">The token's role.</param>
/// <param name="Text">SQL-ready text for operators/parens; raw identifier/number/string content otherwise.</param>
internal sealed record CheckToken(CheckTokenKind Kind, string Text);

/// <summary>
/// A declared constraint (Feature 012): function-call form attached to a member or an entity,
/// e.g. <c>constraint unique as users_email_key</c>, <c>constraint range(min = 0, max = 130)</c>,
/// <c>constraint unique on (a, b)</c>, <c>constraint check (start &lt;= end)</c>.
/// </summary>
/// <param name="Kind">The standard-library constraint kind.</param>
/// <param name="Args">Call arguments (positional or named); empty for zero-arg constraints.</param>
/// <param name="Targets">Member names for an entity-level <c>on (…)</c> constraint; empty for member-level.</param>
/// <param name="CheckTokens">Captured tokens of a <c>check (…)</c> expression; empty otherwise.</param>
/// <param name="SqlName">Explicit SQL constraint name from <c>as {name}</c>; null ⇒ deterministic default.</param>
/// <param name="Location">Source location (for diagnostics).</param>
internal sealed record ConstraintModel(
    ConstraintKind Kind,
    EquatableArray<ConstraintArg> Args,
    EquatableArray<string> Targets,
    EquatableArray<CheckToken> CheckTokens,
    string? SqlName,
    LocationInfo Location
);

/// <summary>
/// A declared annotation (Feature 012): metadata, not validation. Same function-call form as a
/// constraint, e.g. <c>annotation column("email_addr")</c>. The <c>column</c> annotation supplies the
/// database column name (replacing the old <c>db("…")</c> member modifier on the DSL surface).
/// </summary>
/// <param name="Name">The annotation name (e.g. <c>column</c>).</param>
/// <param name="Args">Call arguments (positional or named).</param>
/// <param name="Location">Source location (for diagnostics).</param>
internal sealed record AnnotationModel(
    string Name,
    EquatableArray<ConstraintArg> Args,
    LocationInfo Location
);

/// <summary>
/// A custom scalar type (Feature 012 US4): <c>scalar Name extending Base { constraint…; }</c>.
/// A member typed with the scalar becomes a value property of <paramref name="BaseDslType"/> that
/// inherits the scalar's <paramref name="Constraints"/>. Resolved at parse time (no distinct CLR type).
/// </summary>
/// <param name="Name">The scalar type name (e.g. <c>Username</c>).</param>
/// <param name="BaseDslType">The base DormantQL value type it extends (e.g. <c>str</c>).</param>
/// <param name="Constraints">Constraints carried by the scalar, applied to every member of this type.</param>
/// <param name="Location">Source location (for diagnostics).</param>
internal sealed record ScalarTypeModel(
    string Name,
    string BaseDslType,
    EquatableArray<ConstraintModel> Constraints,
    LocationInfo Location
);

/// <summary>A declared entity.</summary>
/// <param name="Name">The entity (type) name.</param>
/// <param name="Properties">Scalar/value properties, in source order.</param>
/// <param name="References">Relationship references, in source order.</param>
/// <param name="NameOverride">Explicit database table name (<c>db("…")</c>); overrides the convention (FR-054).</param>
/// <param name="IsAbstract">Whether the entity is abstract (emits no table; Feature 012 US5).</param>
/// <param name="Extends">Base entity names this entity extends/composes (Feature 012 US5).</param>
/// <param name="Constraints">Entity-level constraints (multi-field / <c>check</c>; Feature 012 US2).</param>
/// <param name="Annotations">Entity-level annotations (Feature 012).</param>
internal sealed record EntityModel(
    string Name,
    EquatableArray<PropertyModel> Properties,
    EquatableArray<ReferenceModel> References,
    string? NameOverride = null,
    bool IsAbstract = false,
    EquatableArray<string> Extends = default,
    EquatableArray<ConstraintModel> Constraints = default,
    EquatableArray<AnnotationModel> Annotations = default
);

/// <summary>A declared value property.</summary>
/// <param name="Name">The DormantQL property name.</param>
/// <param name="DslType">The DormantQL value type as written (e.g. <c>str</c>).</param>
/// <param name="ClrType">The mapped CLR type (e.g. <c>string</c>), or empty when the type is unknown.</param>
/// <param name="IsNullable">Whether the property is nullable (declared with a trailing <c>?</c>).</param>
/// <param name="IsPrimary">Whether the property is part of the primary key.</param>
/// <param name="IsConcurrency">Whether the property is the optimistic-concurrency token.</param>
/// <param name="NameOverride">Resolved database column name (from a <c>column(…)</c> annotation); overrides the convention (FR-054).</param>
/// <param name="Constraints">Member-level constraints from the <c>{ … }</c> block (Feature 012).</param>
/// <param name="Annotations">Member-level annotations from the <c>{ … }</c> block (Feature 012).</param>
internal sealed record PropertyModel(
    string Name,
    string DslType,
    string ClrType,
    bool IsNullable,
    bool IsPrimary,
    bool IsConcurrency,
    string? NameOverride = null,
    EquatableArray<ConstraintModel> Constraints = default,
    EquatableArray<AnnotationModel> Annotations = default
);

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
    LocationInfo TargetLocation
);
