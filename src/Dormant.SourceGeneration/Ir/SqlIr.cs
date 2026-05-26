using System.Collections.Generic;

namespace Dormant.SourceGeneration.Ir;

/// <summary>
/// A structured, provider-neutral intermediate representation of the SQL statements the generator produces
/// (spec FR-059/FR-060; 005 FR-003). Emitters build these nodes — with database names already resolved
/// (FR-052/FR-054) but with NO dialect lexical choices baked in (no quoting style, placeholder form, casts,
/// type names, or native-function spelling) — and a per-dialect <see cref="Dialects.ISqlDialectRenderer"/>
/// renders each node to text at build time, once per target dialect. Nodes are emit-time scaffolding
/// (downstream of the cached parse models), so determinism — not pipeline equatability — is what matters.
/// </summary>
internal abstract record SqlStatement;

/// <summary>A (optionally schema-qualified) table reference; names already resolved (FR-045/FR-052).</summary>
internal sealed record TableRef(string? Schema, string Name);

/// <summary>A column definition for DDL: resolved name, DormantQL value type (the renderer maps it to a
/// dialect SQL type), nullability, key.</summary>
internal sealed record ColumnDef(string Name, string DslType, bool NotNull, bool PrimaryKey);

/// <summary>
/// A value supplied to an INSERT/UPDATE: a bound positional parameter (optionally a JSON value, which some
/// dialects cast) or an inline native function call. The renderer turns it into dialect text — keeping
/// <c>$1::jsonb</c> / <c>?</c> / <c>now()</c> spelling out of the neutral IR (005 D8/D10).
/// </summary>
internal abstract record SqlValue;

/// <summary>A bound positional parameter at one-based <paramref name="Index"/>; <paramref name="Json"/> marks a JSON value.</summary>
internal sealed record ParamValue(int Index, bool Json) : SqlValue;

/// <summary>An inline native function call (e.g. <c>now</c>) the dialect spells out.</summary>
internal sealed record NativeValue(string Func) : SqlValue;

/// <summary>An INSERT with one <see cref="SqlValue"/> per column (declaration order) and an optional <c>RETURNING</c> list.</summary>
internal sealed record InsertStatement(
    TableRef Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<SqlValue> Values,
    IReadOnlyList<string>? Returning = null
) : SqlStatement;

/// <summary>A SELECT with an optional WHERE / ORDER BY / LIMIT / OFFSET.</summary>
internal sealed record SelectStatement(
    TableRef Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<SqlOrder> OrderBy,
    SqlLimit? Limit,
    SqlLimit? Offset
) : SqlStatement;

/// <summary>A DELETE with a WHERE clause and an optional <c>RETURNING</c> column list (003 FR-017).</summary>
internal sealed record DeleteStatement(
    TableRef Table,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<string>? Returning = null
) : SqlStatement;

/// <summary>A SET assignment in an UPDATE: <c>"column" = &lt;value&gt;</c> (value rendered per dialect).</summary>
internal sealed record SqlAssignment(string Column, SqlValue Value);

/// <summary>An UPDATE with SET assignments, a WHERE clause, and an optional <c>RETURNING</c> list (003 FR-017).</summary>
internal sealed record UpdateStatement(
    TableRef Table,
    IReadOnlyList<SqlAssignment> Assignments,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<string>? Returning = null
) : SqlStatement;

/// <summary>A <c>CREATE SCHEMA IF NOT EXISTS</c> for a module's database schema (FR-045).</summary>
internal sealed record CreateSchemaStatement(string Schema) : SqlStatement;

/// <summary>A <c>CREATE TABLE IF NOT EXISTS</c> with column definitions (FR-020/FR-045).</summary>
internal sealed record CreateTableStatement(TableRef Table, IReadOnlyList<ColumnDef> Columns)
    : SqlStatement;

/// <summary>A <c>"column" op $index</c> comparison (database column name already resolved; operator is the
/// canonical SQL keyword — the renderer remaps where a dialect differs, e.g. <c>ILIKE</c> → <c>LIKE</c>).</summary>
internal sealed record SqlCondition(string Column, string Operator, int ParameterIndex);

/// <summary>An ORDER BY term.</summary>
internal sealed record SqlOrder(string Column, bool Descending);

/// <summary>A LIMIT/OFFSET value: an integer literal or a positional parameter.</summary>
internal sealed record SqlLimit(bool IsParameter, int ParameterIndex, int Literal);
