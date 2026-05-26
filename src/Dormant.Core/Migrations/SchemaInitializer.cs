using System.Collections.Generic;
using Dormant.Abstractions.Entities;
using Dormant.Abstractions.Providers;
using Dormant.Abstractions.Querying;

namespace Dormant.Core.Migrations;

/// <summary>
/// Applies the generated initial schema (spec FR-020/FR-045): for every registered
/// <see cref="IEntityBinding"/>, creates its module's database schema (once per distinct schema) and
/// its table from the prebuilt, schema-qualified <c>CREATE TABLE IF NOT EXISTS</c> DDL. Idempotent
/// (<c>IF NOT EXISTS</c>). Incremental diffing, rollback, and destructive-op guarding are later slices.
/// </summary>
public static class SchemaInitializer
{
    /// <summary>Creates the schemas and tables for all registered entity bindings within one transaction.</summary>
    /// <param name="db">An open driver session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the schema has been applied.</returns>
    public static async ValueTask EnsureCreatedAsync(
        IDbSession db,
        CancellationToken cancellationToken = default
    )
    {
        var bindings = EntityBindings.All();

        await db.BeginAsync(cancellationToken).ConfigureAwait(false);

        // One CREATE SCHEMA per distinct module schema, rendered for the session's dialect by generated code
        // (empty when the dialect has no schema concept, e.g. SQLite — 005 D5). No dialect SQL lives here.
        var seenSchemas = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!seenSchemas.Add(binding.Schema))
            {
                continue;
            }

            var schemaSql = binding.CreateSchemaSql(db.Dialect);
            if (!string.IsNullOrEmpty(schemaSql))
            {
                await db.ExecuteAsync(new PreparedStatement(schemaSql), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var binding in bindings)
        {
            await db.ExecuteAsync(
                    new PreparedStatement(binding.CreateTableSql(db.Dialect)),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        await db.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
