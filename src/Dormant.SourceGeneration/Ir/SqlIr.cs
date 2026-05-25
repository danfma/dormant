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

/// <summary>An INSERT with positional <c>$1…$n</c> values, one per column (declaration order).</summary>
internal sealed record InsertStatement(string Table, IReadOnlyList<string> Columns) : SqlStatement;

/// <summary>A SELECT with an optional WHERE / ORDER BY / LIMIT / OFFSET.</summary>
internal sealed record SelectStatement(
    string Table,
    IReadOnlyList<string> Columns,
    IReadOnlyList<SqlCondition> Where,
    IReadOnlyList<SqlOrder> OrderBy,
    SqlLimit? Limit,
    SqlLimit? Offset) : SqlStatement;

/// <summary>A DELETE with a WHERE clause.</summary>
internal sealed record DeleteStatement(string Table, IReadOnlyList<SqlCondition> Where) : SqlStatement;

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
        _ => throw new System.NotSupportedException($"Unknown statement: {statement.GetType().Name}"),
    };

    private static string RenderInsert(InsertStatement insert)
    {
        var columns = string.Join(", ", insert.Columns.Select(Quote));
        var values = string.Join(
            ", ",
            insert.Columns.Select((_, i) => "$" + (i + 1).ToString(CultureInfo.InvariantCulture)));
        return $"INSERT INTO {Quote(insert.Table)} ({columns}) VALUES ({values})";
    }

    private static string RenderSelect(SelectStatement select)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("SELECT ").Append(string.Join(", ", select.Columns.Select(Quote)));
        sb.Append(" FROM ").Append(Quote(select.Table));
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
        sb.Append("DELETE FROM ").Append(Quote(delete.Table));
        AppendWhere(sb, delete.Where);
        return sb.ToString();
    }

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
