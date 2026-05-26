using System.Linq;
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

        foreach (
            var schema in bindings.Select(b => b.Schema).Distinct(System.StringComparer.Ordinal)
        )
        {
            await db.ExecuteAsync(
                    new PreparedStatement($"CREATE SCHEMA IF NOT EXISTS \"{schema}\""),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        foreach (var binding in bindings)
        {
            await db.ExecuteAsync(new PreparedStatement(binding.CreateTableSql), cancellationToken)
                .ConfigureAwait(false);
        }

        await db.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
