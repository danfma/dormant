namespace Dormant.SourceGeneration.Ir.Dialects;

/// <summary>
/// A build-time renderer that turns the provider-neutral <see cref="SqlStatement"/> IR into one SQL
/// dialect's text (spec 005 FR-003/FR-004, research D1). The generator owns one renderer per target SQL
/// dialect and renders a variant per registered renderer at build time; generated code then selects the
/// variant by the session's <c>DialectId</c> at runtime (no runtime SQL compilation). Lexical concerns
/// (identifier quoting, placeholders, casts, type names, native functions, schema/table qualification)
/// live here, NOT in the IR — keeping the IR neutral so a future non-SQL strategy can consume it (SC-004).
/// </summary>
internal interface ISqlDialectRenderer
{
    /// <summary>
    /// The <c>DialectId</c> enum member name this renderer targets (e.g. <c>PostgreSql</c>); emitted as
    /// <c>global::Dormant.Abstractions.Providers.DialectId.{EnumMember}</c> in the generated variant switch.
    /// </summary>
    string EnumMember { get; }

    /// <summary>Whether this dialect emits a real <c>CREATE SCHEMA</c> (false ⇒ schemas are folded away).</summary>
    bool RendersCreateSchema { get; }

    /// <summary>Quotes a single identifier (table/column) for this dialect.</summary>
    string Quote(string identifier);

    /// <summary>Renders an (optionally schema-qualified) table reference for this dialect.</summary>
    string QualifyTable(string? schema, string name);

    /// <summary>Maps a DormantQL value type to this dialect's SQL column type.</summary>
    string TypeName(string dslType);

    /// <summary>Renders a statement node to this dialect's SQL text (the single string boundary).</summary>
    string Render(SqlStatement statement);

    /// <summary>
    /// The C# <em>expression</em> (as source text) that builds a positional placeholder at runtime from the
    /// integer index variable <paramref name="indexExpr"/> — used by the dynamic optional-filter path
    /// (fragment selection, not SQL compilation). PostgreSQL ⇒ <c>"$" + n</c>; SQLite ⇒ <c>"?"</c>.
    /// </summary>
    string DynamicPlaceholderExpr(string indexExpr);
}
