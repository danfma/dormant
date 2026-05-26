using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dormant.SourceGeneration.Ir.Dialects;

/// <summary>
/// Shared statement-rendering for SQL dialects: walks the neutral <see cref="SqlStatement"/> IR and defers
/// every lexical choice to the dialect primitives (<see cref="Quote"/>, <see cref="Placeholder"/>,
/// <see cref="JsonCast"/>, <see cref="NativeFunc"/>, <see cref="MapOperator"/>, <see cref="TypeName"/>,
/// <see cref="QualifyTable"/>). Concrete dialects override only what differs (005 D1).
/// </summary>
internal abstract class SqlDialectRendererBase : ISqlDialectRenderer
{
    public abstract string EnumMember { get; }

    public abstract bool RendersCreateSchema { get; }

    public abstract string Quote(string identifier);

    public abstract string TypeName(string dslType);

    public abstract string DynamicPlaceholderExpr(string indexExpr);

    /// <summary>The positional placeholder for a one-based parameter index (e.g. <c>$1</c> or <c>?</c>).</summary>
    protected abstract string Placeholder(int index);

    /// <summary>The cast appended to a JSON parameter (e.g. <c>::jsonb</c>), or empty when none.</summary>
    protected abstract string JsonCast { get; }

    /// <summary>Spells out a native function call (e.g. <c>now</c> → <c>now()</c>).</summary>
    protected abstract string NativeFunc(string func);

    /// <summary>Remaps a canonical SQL operator where this dialect differs (default: identity).</summary>
    protected virtual string MapOperator(string op) => op;

    public virtual string QualifyTable(string? schema, string name) =>
        schema is null ? Quote(name) : Quote(schema) + "." + Quote(name);

    public string Render(SqlStatement statement) =>
        statement switch
        {
            InsertStatement insert => RenderInsert(insert),
            SelectStatement select => RenderSelect(select),
            DeleteStatement delete => RenderDelete(delete),
            UpdateStatement update => RenderUpdate(update),
            CreateSchemaStatement createSchema => RendersCreateSchema
                ? "CREATE SCHEMA IF NOT EXISTS " + Quote(createSchema.Schema)
                : string.Empty,
            CreateTableStatement createTable => RenderCreateTable(createTable),
            _ => throw new System.NotSupportedException(
                $"Unknown statement: {statement.GetType().Name}"
            ),
        };

    protected string RenderValue(SqlValue value) =>
        value switch
        {
            ParamValue p => Placeholder(p.Index) + (p.Json ? JsonCast : string.Empty),
            NativeValue n => NativeFunc(n.Func),
            _ => throw new System.NotSupportedException($"Unknown value: {value.GetType().Name}"),
        };

    private string RenderInsert(InsertStatement insert)
    {
        var columns = string.Join(", ", insert.Columns.Select(Quote));
        var values = string.Join(", ", insert.Values.Select(RenderValue));
        var sql =
            $"INSERT INTO {QualifyTable(insert.Table.Schema, insert.Table.Name)} ({columns}) VALUES ({values})";
        if (insert.Returning is { Count: > 0 })
        {
            sql += " RETURNING " + string.Join(", ", insert.Returning.Select(Quote));
        }

        return sql;
    }

    private string RenderCreateTable(CreateTableStatement createTable)
    {
        var columns = string.Join(
            ", ",
            createTable.Columns.Select(c =>
            {
                var parts = Quote(c.Name) + " " + TypeName(c.DslType);
                if (c.NotNull)
                {
                    parts += " NOT NULL";
                }

                if (c.PrimaryKey)
                {
                    parts += " PRIMARY KEY";
                }

                return parts;
            })
        );
        return $"CREATE TABLE IF NOT EXISTS {QualifyTable(createTable.Table.Schema, createTable.Table.Name)} ({columns})";
    }

    private string RenderSelect(SelectStatement select)
    {
        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(string.Join(", ", select.Columns.Select(Quote)));
        sb.Append(" FROM ").Append(QualifyTable(select.Table.Schema, select.Table.Name));
        AppendWhere(sb, select.Where);

        if (select.OrderBy.Count > 0)
        {
            sb.Append(" ORDER BY ");
            sb.Append(
                string.Join(
                    ", ",
                    select.OrderBy.Select(o => Quote(o.Column) + (o.Descending ? " DESC" : " ASC"))
                )
            );
        }

        AppendLimit(sb, "LIMIT", select.Limit);
        AppendLimit(sb, "OFFSET", select.Offset);
        return sb.ToString();
    }

    private string RenderDelete(DeleteStatement delete)
    {
        var sb = new StringBuilder();
        sb.Append("DELETE FROM ").Append(QualifyTable(delete.Table.Schema, delete.Table.Name));
        AppendWhere(sb, delete.Where);
        AppendReturning(sb, delete.Returning);
        return sb.ToString();
    }

    private string RenderUpdate(UpdateStatement update)
    {
        var sb = new StringBuilder();
        sb.Append("UPDATE ")
            .Append(QualifyTable(update.Table.Schema, update.Table.Name))
            .Append(" SET ");
        sb.Append(
            string.Join(
                ", ",
                update.Assignments.Select(a => Quote(a.Column) + " = " + RenderValue(a.Value))
            )
        );
        AppendWhere(sb, update.Where);
        AppendReturning(sb, update.Returning);
        return sb.ToString();
    }

    private void AppendReturning(StringBuilder sb, IReadOnlyList<string>? columns)
    {
        if (columns is null || columns.Count == 0)
        {
            return;
        }

        sb.Append(" RETURNING ").Append(string.Join(", ", columns.Select(Quote)));
    }

    private void AppendWhere(StringBuilder sb, IReadOnlyList<SqlCondition> where)
    {
        if (where.Count == 0)
        {
            return;
        }

        sb.Append(" WHERE ");
        sb.Append(
            string.Join(
                " AND ",
                where.Select(c =>
                    $"{Quote(c.Column)} {MapOperator(c.Operator)} {Placeholder(c.ParameterIndex)}"
                )
            )
        );
    }

    private void AppendLimit(StringBuilder sb, string keyword, SqlLimit? limit)
    {
        if (limit is null)
        {
            return;
        }

        sb.Append(' ').Append(keyword).Append(' ');
        sb.Append(
            limit.IsParameter
                ? Placeholder(limit.ParameterIndex)
                : limit.Literal.ToString(CultureInfo.InvariantCulture)
        );
    }
}
