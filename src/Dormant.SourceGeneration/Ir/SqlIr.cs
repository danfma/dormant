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
/// dialect SQL type), nullability, key, and an optional literal DEFAULT (e.g. the concurrency token, 012).</summary>
internal sealed record ColumnDef(
    string Name,
    string DslType,
    bool NotNull,
    bool PrimaryKey,
    string? Default = null
);

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

/// <summary>The kind of a table-level constraint (Feature 012).</summary>
internal enum ConstraintIrKind
{
    /// <summary>Composite PRIMARY KEY (single-column PK stays inline on the column).</summary>
    PrimaryKey,

    /// <summary>UNIQUE over one or more columns.</summary>
    Unique,

    /// <summary>CHECK with a dialect-neutral boolean expression (<see cref="ConstraintDef.CheckSql"/>).</summary>
    Check,

    /// <summary>Regular-expression match on a single column (<see cref="ConstraintDef.CheckSql"/> holds the
    /// pattern). Rendered per dialect: PostgreSQL <c>~</c>; SQLite has no native REGEXP and omits it.</summary>
    Regex,
}

/// <summary>
/// A table-level constraint emitted into <c>CREATE TABLE</c> (Feature 012). Names are resolved
/// (explicit <c>as</c> or a deterministic default); <paramref name="CheckSql"/> is dialect-neutral
/// SQL (column names already quoted by the renderer's helper) used only for <see cref="ConstraintIrKind.Check"/>.
/// </summary>
internal sealed record ConstraintDef(
    ConstraintIrKind Kind,
    IReadOnlyList<string> Columns,
    string? CheckSql,
    string Name
);

/// <summary>A <c>CREATE TABLE IF NOT EXISTS</c> with column definitions and optional table-level
/// constraints (FR-020/FR-045; constraints from Feature 012).</summary>
internal sealed record CreateTableStatement(
    TableRef Table,
    IReadOnlyList<ColumnDef> Columns,
    IReadOnlyList<ConstraintDef>? TableConstraints = null
) : SqlStatement;

/// <summary>A <c>"column" op $index</c> comparison (database column name already resolved; operator is the
/// canonical SQL keyword — the renderer remaps where a dialect differs, e.g. <c>ILIKE</c> → <c>LIKE</c>).</summary>
internal sealed record SqlCondition(string Column, string Operator, int ParameterIndex);

/// <summary>An ORDER BY term.</summary>
internal sealed record SqlOrder(string Column, bool Descending);

/// <summary>A LIMIT/OFFSET value: an integer literal or a positional parameter.</summary>
internal sealed record SqlLimit(bool IsParameter, int ParameterIndex, int Literal);

// ---------------------------------------------------------------------------------------------------
// Relational / expression IR (009 P-B). The flat single-table SelectStatement above is kept for the
// common case (byte-identical output); the nodes below express the multi-table joins + qualified
// columns that relationship navigation (and, later, shape selection) require. Like the rest of the IR,
// names are resolved and no dialect lexical choice is baked in — the renderer spells joins, qualified
// columns, and operators per dialect.
// ---------------------------------------------------------------------------------------------------

/// <summary>An (optionally schema-qualified) table bound to an alias in a relational FROM/JOIN.</summary>
internal sealed record FromItem(TableRef Table, string Alias);

/// <summary>The kind of join.</summary>
internal enum JoinKind
{
    /// <summary><c>INNER JOIN</c>.</summary>
    Inner,

    /// <summary><c>LEFT JOIN</c> (the default for to-one navigation — an absent ref yields nulls).</summary>
    Left,
}

/// <summary>An expression over qualified columns / bound parameters (select-items, join ON, WHERE).</summary>
internal abstract record SqlExpr;

/// <summary>An alias-qualified column reference, rendered as <c>"alias"."column"</c>.</summary>
internal sealed record ColumnExpr(string Alias, string Column) : SqlExpr;

/// <summary>A one-based positional bound parameter.</summary>
internal sealed record ParamExpr(int Index) : SqlExpr;

/// <summary>A binary expression; <paramref name="Operator"/> is the canonical SQL keyword (renderer remaps).</summary>
internal sealed record BinaryExpr(SqlExpr Left, string Operator, SqlExpr Right) : SqlExpr;

/// <summary>A JOIN onto an aliased table with an ON predicate.</summary>
internal sealed record SqlJoin(FromItem Target, JoinKind Kind, SqlExpr On);

/// <summary>A select-list item: an expression with an optional output alias.</summary>
internal sealed record SqlSelectItem(SqlExpr Expr, string? Alias = null);

/// <summary>An ORDER BY term over an expression.</summary>
internal sealed record SqlOrderExpr(SqlExpr Expr, bool Descending);

/// <summary>One key/value member of a <see cref="JsonObjectExpr"/>.</summary>
internal sealed record JsonMember(string Key, SqlExpr Value);

/// <summary>
/// A JSON object built from key/value pairs (009 US1 to-many element). Rendered per dialect
/// (PostgreSQL <c>jsonb_build_object</c>, SQLite <c>json_object</c>).
/// </summary>
internal sealed record JsonObjectExpr(IReadOnlyList<JsonMember> Members) : SqlExpr;

/// <summary>
/// A correlated subquery aggregating a child relation into a JSON array of objects (a to-many shape
/// node). Renders to <c>(SELECT coalesce(&lt;agg&gt;(&lt;element&gt;), '[]') FROM &lt;from&gt; WHERE &lt;where&gt;)</c>
/// — one round-trip, no N+1 (009 US1, research R1).
/// </summary>
internal sealed record JsonArrayAggSubquery(
    FromItem From,
    JsonObjectExpr Element,
    IReadOnlyList<SqlExpr> Where
) : SqlExpr;

/// <summary>
/// A relational SELECT: an aliased FROM, zero or more JOINs, expression select-items, a conjunctive
/// WHERE, ORDER BY, and LIMIT/OFFSET. Emitted when a query navigates relationships; the flat
/// <see cref="SelectStatement"/> remains for single-table queries so their output stays byte-identical.
/// </summary>
internal sealed record JoinedSelectStatement(
    FromItem From,
    IReadOnlyList<SqlJoin> Joins,
    IReadOnlyList<SqlSelectItem> Items,
    IReadOnlyList<SqlExpr> Where,
    IReadOnlyList<SqlOrderExpr> OrderBy,
    SqlLimit? Limit,
    SqlLimit? Offset
) : SqlStatement;
