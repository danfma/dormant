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

    /// <summary>The JSON-object builder function (e.g. <c>jsonb_build_object</c> / <c>json_object</c>).</summary>
    protected abstract string JsonObjectFunc { get; }

    /// <summary>The JSON-array aggregate function (e.g. <c>jsonb_agg</c> / <c>json_group_array</c>).</summary>
    protected abstract string JsonArrayAggFunc { get; }

    /// <summary>The empty-JSON-array literal used to coalesce an aggregate over no rows.</summary>
    protected abstract string EmptyJsonArray { get; }

    public virtual string QualifyTable(string? schema, string name) =>
        schema is null ? Quote(name) : Quote(schema) + "." + Quote(name);

    public string Render(SqlStatement statement) =>
        statement switch
        {
            InsertStatement insert => RenderInsert(insert),
            SelectStatement select => RenderSelect(select),
            JoinedSelectStatement joined => RenderJoinedSelect(joined),
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
        var parts = new List<string>();
        foreach (var c in createTable.Columns)
        {
            var col = Quote(c.Name) + " " + TypeName(c.DslType);
            if (c.NotNull)
            {
                col += " NOT NULL";
            }

            if (c.PrimaryKey)
            {
                col += " PRIMARY KEY";
            }

            parts.Add(col);
        }

        // Feature 012: table-level constraints (named UNIQUE / CHECK / composite PRIMARY KEY).
        // Both PostgreSQL and SQLite accept inline named constraints in CREATE TABLE, so the
        // rendering is shared; dialect-specific cases (e.g. regex) are added by overrides later.
        if (createTable.TableConstraints is { Count: > 0 } constraints)
        {
            foreach (var c in constraints)
            {
                var rendered = RenderConstraint(c);
                if (!string.IsNullOrEmpty(rendered))
                {
                    parts.Add(rendered);
                }
            }
        }

        var body = string.Join(", ", parts);
        return $"CREATE TABLE IF NOT EXISTS {QualifyTable(createTable.Table.Schema, createTable.Table.Name)} ({body})";
    }

    /// <summary>Renders one table-level constraint. Shared across dialects (named inline constraints).</summary>
    protected virtual string RenderConstraint(ConstraintDef constraint)
    {
        var name = "CONSTRAINT " + Quote(constraint.Name) + " ";
        switch (constraint.Kind)
        {
            case ConstraintIrKind.PrimaryKey:
                return name + "PRIMARY KEY (" + RenderColumnList(constraint.Columns) + ")";
            case ConstraintIrKind.Unique:
                return name + "UNIQUE (" + RenderColumnList(constraint.Columns) + ")";
            case ConstraintIrKind.Check:
                return name + "CHECK (" + constraint.CheckSql + ")";
            case ConstraintIrKind.Regex:
                return RenderRegexConstraint(constraint);
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Renders a regex constraint. Default: no DB-level enforcement (returned empty), for dialects
    /// without a native regex operator (e.g. SQLite). PostgreSQL overrides this to a <c>~</c> CHECK.
    /// </summary>
    protected virtual string RenderRegexConstraint(ConstraintDef constraint) => string.Empty;

    private string RenderColumnList(IReadOnlyList<string> columns) =>
        string.Join(", ", columns.Select(Quote));

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

    // 009 P-B: a relational SELECT with aliased FROM, JOINs, and qualified-column expressions.
    private string RenderJoinedSelect(JoinedSelectStatement select)
    {
        var sb = new StringBuilder();
        sb.Append("SELECT ").Append(string.Join(", ", select.Items.Select(RenderSelectItem)));
        sb.Append(" FROM ").Append(RenderFromItem(select.From));

        foreach (var join in select.Joins)
        {
            sb.Append(join.Kind == JoinKind.Left ? " LEFT JOIN " : " INNER JOIN ")
                .Append(RenderFromItem(join.Target))
                .Append(" ON ")
                .Append(RenderExpr(join.On));
        }

        if (select.Where.Count > 0)
        {
            sb.Append(" WHERE ").Append(string.Join(" AND ", select.Where.Select(RenderExpr)));
        }

        if (select.OrderBy.Count > 0)
        {
            sb.Append(" ORDER BY ");
            sb.Append(
                string.Join(
                    ", ",
                    select.OrderBy.Select(o =>
                        RenderExpr(o.Expr) + (o.Descending ? " DESC" : " ASC")
                    )
                )
            );
        }

        AppendLimit(sb, "LIMIT", select.Limit);
        AppendLimit(sb, "OFFSET", select.Offset);
        return sb.ToString();
    }

    private string RenderFromItem(FromItem from) =>
        QualifyTable(from.Table.Schema, from.Table.Name) + " " + Quote(from.Alias);

    private string RenderSelectItem(SqlSelectItem item) =>
        item.Alias is null
            ? RenderExpr(item.Expr)
            : RenderExpr(item.Expr) + " AS " + Quote(item.Alias);

    private string RenderExpr(SqlExpr expr) =>
        expr switch
        {
            ColumnExpr c => Quote(c.Alias) + "." + Quote(c.Column),
            ParamExpr p => Placeholder(p.Index),
            BinaryExpr b => RenderExpr(b.Left)
                + " "
                + MapOperator(b.Operator)
                + " "
                + RenderExpr(b.Right),
            JsonObjectExpr o => JsonObjectFunc
                + "("
                + string.Join(
                    ", ",
                    o.Members.Select(m => "'" + m.Key + "', " + RenderExpr(m.Value))
                )
                + ")",
            JsonArrayAggSubquery s => "(SELECT coalesce("
                + JsonArrayAggFunc
                + "("
                + RenderExpr(s.Element)
                + "), "
                + EmptyJsonArray
                + ") FROM "
                + RenderFromItem(s.From)
                + (
                    s.Where.Count > 0
                        ? " WHERE " + string.Join(" AND ", s.Where.Select(RenderExpr))
                        : string.Empty
                )
                + ")",
            _ => throw new System.NotSupportedException(
                $"Unknown expression: {expr.GetType().Name}"
            ),
        };

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
