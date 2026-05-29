using System.Collections.Generic;
using System.Globalization;

namespace Dormant.SourceGeneration.Ir.Dialects;

/// <summary>
/// The PostgreSQL dialect renderer: double-quoted identifiers, positional <c>$n</c> placeholders,
/// <c>::jsonb</c> casts, real <c>CREATE SCHEMA</c>, and PostgreSQL column types. Its output is byte-identical
/// to the pre-005 single static renderer (the regression guard for the dialect refactor).
/// </summary>
internal sealed class PostgreSqlRenderer : SqlDialectRendererBase
{
    public static readonly PostgreSqlRenderer Instance = new();

    public override string EnumMember => "PostgreSql";

    public override bool RendersCreateSchema => true;

    public override string Quote(string identifier) => "\"" + identifier + "\"";

    // Feature 012: PostgreSQL has a native regex match operator (~), so a `regex` constraint becomes
    // a CHECK. SQLite has no native REGEXP and inherits the base (no enforcement).
    protected override string RenderRegexConstraint(ConstraintDef constraint)
    {
        var col = Quote(constraint.Columns[0]);
        var pattern = (constraint.CheckSql ?? string.Empty).Replace("'", "''");
        return "CONSTRAINT " + Quote(constraint.Name) + " CHECK (" + col + " ~ '" + pattern + "')";
    }

    protected override string Placeholder(int index) =>
        "$" + index.ToString(CultureInfo.InvariantCulture);

    protected override string JsonCast => "::jsonb";

    protected override string JsonObjectFunc => "jsonb_build_object";

    protected override string JsonArrayAggFunc => "jsonb_agg";

    protected override string EmptyJsonArray => "'[]'::jsonb";

    protected override string NativeFunc(string func) =>
        func switch
        {
            "now" => "now()",
            _ => func + "()",
        };

    public override string DynamicPlaceholderExpr(string indexExpr) =>
        "\"$\" + "
        + indexExpr
        + ".ToString(global::System.Globalization.CultureInfo.InvariantCulture)";

    // DormantQL value type → PostgreSQL column type (FR-020). Falls back to text.
    private static readonly Dictionary<string, string> SqlMap = new(System.StringComparer.Ordinal)
    {
        ["string"] = "text",
        ["long"] = "bigint",
        ["double"] = "double precision",
        ["date"] = "date",
        ["str"] = "text",
        ["bool"] = "boolean",
        ["int16"] = "smallint",
        ["int32"] = "integer",
        ["int"] = "integer",
        ["int64"] = "bigint",
        ["float32"] = "real",
        ["float64"] = "double precision",
        ["decimal"] = "numeric",
        ["bigint"] = "numeric",
        ["uuid"] = "uuid",
        ["datetime"] = "timestamptz",
        ["duration"] = "interval",
        ["bytes"] = "bytea",
        ["json"] = "jsonb",
    };

    public override string TypeName(string dslType) =>
        SqlMap.TryGetValue(dslType, out var sql) ? sql : "text";
}
