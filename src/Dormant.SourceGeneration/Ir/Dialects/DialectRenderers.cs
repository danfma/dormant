using System.Collections.Generic;

namespace Dormant.SourceGeneration.Ir.Dialects;

/// <summary>
/// The set of SQL dialect renderers the generator emits variants for (005 D2/D13). The per-statement
/// variant <c>switch</c> emitted into generated code has one arm per renderer here; runtime selects by the
/// session's <c>DialectId</c>. v1 registers PostgreSQL only; the SQLite renderer is added in User Story 1,
/// at which point every authored unit gains its SQLite arm with no further emitter change.
/// </summary>
internal static class DialectRenderers
{
    /// <summary>The registered renderers, in deterministic emission order.</summary>
    public static readonly IReadOnlyList<ISqlDialectRenderer> All = new ISqlDialectRenderer[]
    {
        PostgreSqlRenderer.Instance,
        SqliteRenderer.Instance,
    };
}
