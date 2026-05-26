using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dormant.SourceGeneration.Ir;

/// <summary>
/// A structured intermediate representation of the SQL statements the generator produces (spec
/// FR-059/FR-060). Emitters build these nodes — with database names already resolved (FR-052/FR-054) —
/// instead of assembling strings by hand; <see cref="SqlRenderer"/> renders them to text at the single
/// output boundary, centralizing quoting/formatting. Nodes are emit-time scaffolding (downstream of the
/// cached parse models), so determinism — not pipeline equatability — is what matters here.
/// </summary>
internal abstract record SqlStatement;

/// <summary>A (optionally schema-qualified) table reference; names already resolved (FR-045/FR-052).</summary>
internal sealed record TableRef(string? Schema, string Name);

/// <summary>A column definition for DDL: resolved name, provider SQL type, nullability, key.</summary>
internal sealed record ColumnDef(string Name, string SqlType, bool NotNull, bool PrimaryKey);

/// <summary>An INSERT column: resolved name + optional parameter cast (e.g. <c>jsonb</c> → <c>$1::jsonb</c>).</summary>
internal sealed record InsertColumn(string Name, string? ParamCast);

/// <summary>An INSERT with positional <c>$1…$n</c> values, one per column (declaration order).</summary>
internal sealed record InsertStatement(TableRef Table, IReadOnlyList<InsertColumn> Columns) : SqlStatement;

/// <summary>A SELECT with an optional WHERE / ORDER BY / LIMIT / OFFSET.</summary>
internal sealed record SelectStatement(
    TableRef Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<SqlOrder> OrderBy,
    SqlLimit? Limit,
    SqlLimit? Offset) : SqlStatement;

/// <summary>A DELETE with a WHERE clause and an optional <c>RETURNING</c> column list (003 FR-017).</summary>
internal sealed record DeleteStatement(
    TableRef Table,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<string>? Returning = null) : SqlStatement;

/// <summary>A SET assignment in an UPDATE: <c>"column" = &lt;valueToken&gt;</c> (token already rendered, e.g. <c>$1</c>).</summary>
internal sealed record SqlAssignment(string Column, string ValueToken);

/// <summary>An UPDATE with SET assignments, a WHERE clause, and an optional <c>RETURNING</c> list (003 FR-017).</summary>
internal sealed record UpdateStatement(
    TableRef Table,
    IReadOnlyList<SqlAssignment> Assignments,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<string>? Returning = null) : SqlStatement;

/// <summary>A <c>CREATE SCHEMA IF NOT EXISTS</c> for a module's database schema (FR-045).</summary>
internal sealed record CreateSchemaStatement(string Schema) : SqlStatement;

/// <summary>A <c>CREATE TABLE IF NOT EXISTS</c> with column definitions (FR-020/FR-045).</summary>
internal sealed record CreateTableStatement(TableRef Table, IReadOnlyList<ColumnDef> Columns) : SqlStatement;

/// <summary>A <c>"column" op $index</c> comparison (database column name already resolved).</summary>
internal sealed record SqlCondition(string Column, string Operator, int ParameterIndex);

/// <summary>An ORDER BY term.</summary>
internal sealed record SqlOrder(string Column, bool Descending);

/// <summary>A LIMIT/OFFSET value: an integer literal or a positional parameter.</summary>
internal sealed record SqlLimit(bool IsParameter, int ParameterIndex, int Literal);

/// <summary>Renders <see cref="SqlStatement"/> nodes to PostgreSQL text (the single string boundary).</summary>
internal static class SqlRenderer
{
    public static string Render(SqlStatement statement) => statement switch
    {
        InsertStatement insert => RenderInsert(insert),
        SelectStatement select => RenderSelect(select),
        DeleteStatement delete => RenderDelete(delete),
        UpdateStatement update => RenderUpdate(update),
        CreateSchemaStatement createSchema => $"CREATE SCHEMA IF NOT EXISTS {Quote(createSchema.Schema)}",
        CreateTableStatement createTable => RenderCreateTable(createTable),
        _ => throw new System.NotSupportedException($"Unknown statement: {statement.GetType().Name}"),
    };

    private static string RenderInsert(InsertStatement insert)
    {
        var columns = string.Join(", ", insert.Columns.Select(c => Quote(c.Name)));
        var values = string.Join(
            ", ",
            insert.Columns.Select((c, i) =>
            {
                var placeholder = "$" + (i + 1).ToString(CultureInfo.InvariantCulture);
                return c.ParamCast is null ? placeholder : placeholder + "::" + c.ParamCast;
            }));
        return $"INSERT INTO {RenderTable(insert.Table)} ({columns}) VALUES ({values})";
    }

    private static string RenderCreateTable(CreateTableStatement createTable)
    {
        var columns = string.Join(
            ", ",
            createTable.Columns.Select(c =>
            {
                var parts = Quote(c.Name) + " " + c.SqlType;
                if (c.NotNull)
                {
                    parts += " NOT NULL";
                }

                if (c.PrimaryKey)
                {
                    parts += " PRIMARY KEY";
                }

                return parts;
            }));
        return $"CREATE TABLE IF NOT EXISTS {RenderTable(createTable.Table)} ({columns})";
    }

    private static string RenderSelect(SelectStatement select)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("SELECT ").Append(string.Join(", ", select.Columns.Select(Quote)));
        sb.Append(" FROM ").Append(RenderTable(select.Table));
        AppendWhere(sb, select.Where);

        if (select.OrderBy.Count > 0)
        {
            sb.Append(" ORDER BY ");
            sb.Append(string.Join(
                ", ",
                select.OrderBy.Select(o => Quote(o.Column) + (o.Descending ? " DESC" : " ASC"))));
        }

        AppendLimit(sb, "LIMIT", select.Limit);
        AppendLimit(sb, "OFFSET", select.Offset);
        return sb.ToString();
    }

    private static string RenderDelete(DeleteStatement delete)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("DELETE FROM ").Append(RenderTable(delete.Table));
        AppendWhere(sb, delete.Where);
        AppendReturning(sb, delete.Returning);
        return sb.ToString();
    }

    private static string RenderUpdate(UpdateStatement update)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("UPDATE ").Append(RenderTable(update.Table)).Append(" SET ");
        sb.Append(string.Join(", ", update.Assignments.Select(a => Quote(a.Column) + " = " + a.ValueToken)));
        AppendWhere(sb, update.Where);
        AppendReturning(sb, update.Returning);
        return sb.ToString();
    }

    private static void AppendReturning(System.Text.StringBuilder sb, IReadOnlyList<string>? columns)
    {
        if (columns is null || columns.Count == 0)
        {
            return;
        }

        sb.Append(" RETURNING ").Append(string.Join(", ", columns.Select(Quote)));
    }

    private static string RenderTable(TableRef table) =>
        table.Schema is null ? Quote(table.Name) : Quote(table.Schema) + "." + Quote(table.Name);

    private static void AppendWhere(System.Text.StringBuilder sb, IReadOnlyList<SqlCondition> where)
    {
        if (where.Count == 0)
        {
            return;
        }

        sb.Append(" WHERE ");
        sb.Append(string.Join(
            " AND ",
            where.Select(c => $"{Quote(c.Column)} {c.Operator} ${c.ParameterIndex.ToString(CultureInfo.InvariantCulture)}")));
    }

    private static void AppendLimit(System.Text.StringBuilder sb, string keyword, SqlLimit? limit)
    {
        if (limit is null)
        {
            return;
        }

        sb.Append(' ').Append(keyword).Append(' ');
        sb.Append(limit.IsParameter
            ? "$" + limit.ParameterIndex.ToString(CultureInfo.InvariantCulture)
            : limit.Literal.ToString(CultureInfo.InvariantCulture));
    }

    private static string Quote(string identifier) => "\"" + identifier + "\"";
}
