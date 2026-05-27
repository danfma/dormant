using System.Collections.Generic;

namespace Dormant.SourceGeneration.Ir.Dialects;

/// <summary>
/// The SQLite dialect renderer (005 D5–D10): double-quoted identifiers, named <c>@pN</c> placeholders
/// (Microsoft.Data.Sqlite binds by name — reliable regardless of order), no <c>::</c> casts, type affinities
/// (TEXT/INTEGER/REAL/BLOB), <c>ILIKE</c> → <c>LIKE</c>, and no schema concept — a module schema is folded
/// into the table name (<c>"app"."user"</c> → <c>"app_user"</c>) and <c>CREATE SCHEMA</c> renders to nothing.
/// <c>RETURNING</c> is kept (SQLite ≥ 3.35).
/// </summary>
internal sealed class SqliteRenderer : SqlDialectRendererBase
{
    public static readonly SqliteRenderer Instance = new();

    public override string EnumMember => "Sqlite";

    public override bool RendersCreateSchema => false;

    public override string Quote(string identifier) => "\"" + identifier + "\"";

    // SQLite has no schemas: fold the module schema into a single prefixed, quoted table identifier.
    public override string QualifyTable(string? schema, string name) =>
        schema is null ? Quote(name) : Quote(schema + "_" + name);

    protected override string Placeholder(int index) =>
        "@p" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected override string JsonCast => string.Empty;

    protected override string NativeFunc(string func) =>
        func switch
        {
            "now" => "CURRENT_TIMESTAMP",
            _ => func + "()",
        };

    protected override string MapOperator(string op) =>
        string.Equals(op, "ILIKE", System.StringComparison.Ordinal) ? "LIKE" : op;

    protected override string JsonObjectFunc => "json_object";

    protected override string JsonArrayAggFunc => "json_group_array";

    protected override string EmptyJsonArray => "json('[]')";

    public override string DynamicPlaceholderExpr(string indexExpr) =>
        "\"@p\" + "
        + indexExpr
        + ".ToString(global::System.Globalization.CultureInfo.InvariantCulture)";

    // DormantQL value type → SQLite column affinity (005 D6). Falls back to TEXT.
    private static readonly Dictionary<string, string> SqlMap = new(System.StringComparer.Ordinal)
    {
        ["string"] = "TEXT",
        ["long"] = "INTEGER",
        ["double"] = "REAL",
        ["date"] = "TEXT",
        ["str"] = "TEXT",
        ["bool"] = "INTEGER",
        ["int16"] = "INTEGER",
        ["int32"] = "INTEGER",
        ["int"] = "INTEGER",
        ["int64"] = "INTEGER",
        ["float32"] = "REAL",
        ["float64"] = "REAL",
        ["decimal"] = "TEXT",
        ["bigint"] = "TEXT",
        ["uuid"] = "TEXT",
        ["datetime"] = "TEXT",
        ["duration"] = "TEXT",
        ["bytes"] = "BLOB",
        ["json"] = "TEXT",
    };

    public override string TypeName(string dslType) =>
        SqlMap.TryGetValue(dslType, out var sql) ? sql : "TEXT";
}
