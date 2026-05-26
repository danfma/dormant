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
    EquatableArray<DiagnosticInfo> Diagnostics)
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

/// <summary>A single authored write command (v1 MVP: <c>insert Entity { col := expr, … }</c>).</summary>
/// <param name="Name">The command name (becomes the generated method name).</param>
/// <param name="Kind">The command kind.</param>
/// <param name="RootEntity">The target entity.</param>
/// <param name="Parameters">Declared parameters, in source order.</param>
/// <param name="Assignments">Column assignments (<c>col := expr</c>), in source order (insert/update).</param>
/// <param name="Filters">WHERE conditions for <c>update</c>/<c>delete</c> (incl. concurrency-token match).</param>
internal sealed record CommandModel(
    string Name,
    CommandKind Kind,
    string RootEntity,
    EquatableArray<QueryParameter> Parameters,
    EquatableArray<Assignment> Assignments,
    EquatableArray<FilterCondition> Filters = default);

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
}

/// <summary>An assigned value: a parameter reference, a literal, or a native call (FR-008).</summary>
/// <param name="Kind">The value kind.</param>
/// <param name="Text">Parameter name / literal text / native function name (e.g. <c>now</c>).</param>
internal sealed record CommandValue(CommandValueKind Kind, string Text);
