namespace Dormant.Abstractions.Migrations;

/// <summary>Persistence port for migration state: applied set, apply, and revert.</summary>
public interface IMigrationStore
{
    /// <summary>Returns the migrations already applied to the database, in order.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The applied migrations.</returns>
    ValueTask<IReadOnlyList<MigrationRecord>> GetAppliedAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies a migration and records it.</summary>
    /// <param name="migration">The migration to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when applied.</returns>
    ValueTask ApplyAsync(MigrationRecord migration, CancellationToken cancellationToken = default);

    /// <summary>Reverts a previously applied migration.</summary>
    /// <param name="migration">The migration to revert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when reverted.</returns>
    ValueTask RevertAsync(MigrationRecord migration, CancellationToken cancellationToken = default);
}