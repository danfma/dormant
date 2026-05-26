using Dormant.SourceGeneration.Diagnostics;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>Parsed + validated commands for one DormantQL file (<c>.dql</c>). Equatable for caching.</summary>
/// <param name="ModuleName">The declared module (shared with the schema; drives the namespace).</param>
/// <param name="FilePath">The query/command file path (used to derive the generated namespace, FR-046).</param>
/// <param name="Commands">The declared commands, in source order.</param>
/// <param name="Diagnostics">Syntax diagnostics collected while building the model.</param>
internal sealed record CommandFile(
    string ModuleName,
    string FilePath,
    EquatableArray<CommandModel> Commands,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>Whether the file is free of error-severity diagnostics and safe to emit.</summary>
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>The kind of write command (FR-002).</summary>
internal enum CommandKind
{
    /// <summary><c>insert</c></summary>
    Insert,

    /// <summary><c>update</c></summary>
    Update,

    /// <summary><c>delete</c></summary>
    Delete,
}

/// <summary>A single authored write command (003: <c>insert|update|delete Entity alias …</c>).</summary>
/// <param name="Name">The unit's authored snake_case name (becomes a PascalCase method via <see cref="Emit.Naming.ToPascalCase"/>).</param>
/// <param name="Kind">The command kind.</param>
/// <param name="RootEntity">The target entity.</param>
/// <param name="Alias">The subject alias declared after the entity (003 — member refs are alias-qualified).</param>
/// <param name="Parameters">Declared parameters, in source order.</param>
/// <param name="Assignments">Column assignments (<c>alias.col = expr</c>), in source order (insert/update).</param>
/// <param name="Filters">WHERE conditions for <c>update</c>/<c>delete</c> (incl. concurrency-token match).</param>
/// <param name="Returning">Optional explicit result shape (003 FR-017); <see langword="null"/> ⇒ default inference.</param>
/// <param name="Bindings">Preceding <c>with name = (command)</c> bindings (003 FR-021/FR-022); this model is
/// the terminal command. Empty for a plain single-command mutation.</param>
internal sealed record CommandModel(
    string Name,
    CommandKind Kind,
    string RootEntity,
    string Alias,
    EquatableArray<QueryParameter> Parameters,
    EquatableArray<Assignment> Assignments,
    EquatableArray<FilterCondition> Filters = default,
    ReturningShape? Returning = null,
    EquatableArray<WithBinding> Bindings = default
);

/// <summary>A <c>with name = (command)</c> binding: the bound command runs as its own SQL statement and its
/// result (default: the inserted PK) becomes a C# local referable downstream (003 FR-021/FR-022).</summary>
/// <param name="Name">The binding name (a C# local in the generated method).</param>
/// <param name="Command">The bound write command (insert/update/delete).</param>
internal sealed record WithBinding(string Name, CommandModel Command);

/// <summary>The shape of an explicit <c>returning</c> clause (003 FR-017) — mirrors <c>select</c>.</summary>
internal enum ReturningKind
{
    /// <summary><c>returning alias</c> — the full immutable entity.</summary>
    Entity,

    /// <summary><c>returning { alias.a, alias.b }</c> — a distinct projection type.</summary>
    Projection,

    /// <summary><c>returning alias.field</c> — a single scalar value.</summary>
    Scalar,
}

/// <summary>An explicit mutation <c>returning</c> shape. <see cref="Members"/> is empty for
/// <see cref="ReturningKind.Entity"/>, a single member for <see cref="ReturningKind.Scalar"/>, and the
/// projected members for <see cref="ReturningKind.Projection"/> (FR-008/FR-017).</summary>
internal sealed record ReturningShape(ReturningKind Kind, EquatableArray<string> Members);

/// <summary>A column assignment in a command body: <c>column := value</c>.</summary>
/// <param name="Column">The target column (DSL member name).</param>
/// <param name="Value">The assigned value expression.</param>
internal sealed record Assignment(string Column, CommandValue Value);

/// <summary>The kind of assigned value expression (v1 MVP subset).</summary>
internal enum CommandValueKind
{
    /// <summary>A reference to a declared parameter.</summary>
    Parameter,

    /// <summary>A string literal.</summary>
    StringLiteral,

    /// <summary>A numeric literal.</summary>
    NumberLiteral,

    /// <summary>A native function call (e.g. <c>datetime::now()</c>).</summary>
    NativeCall,

    /// <summary>A reference to a <c>with</c>-bound name (003 FR-021); in a ref/FK context it is the target PK.</summary>
    WithRef,
}

/// <summary>An assigned value: a parameter reference, a literal, or a native call (FR-008).</summary>
/// <param name="Kind">The value kind.</param>
/// <param name="Text">Parameter name / literal text / native function name (e.g. <c>now</c>).</param>
internal sealed record CommandValue(CommandValueKind Kind, string Text);
