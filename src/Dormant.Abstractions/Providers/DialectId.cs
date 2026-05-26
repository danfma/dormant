namespace Dormant.Abstractions.Providers;

/// <summary>
/// The SQL dialect a session executes against (spec 005 FR-003/FR-004). The generator renders one SQL
/// variant per member at build time; generated code selects the matching variant by the session's
/// <see cref="IDbSession.Dialect"/> at runtime (a branch over compile-time-constant strings — no runtime SQL
/// compilation). Adding a member is paired with a build-time renderer and a provider adapter; the runtime
/// core needs no change to support a new dialect.
/// </summary>
public enum DialectId
{
    /// <summary>PostgreSQL (the primary reference provider).</summary>
    PostgreSql,

    /// <summary>SQLite (file or in-memory).</summary>
    Sqlite,
}
