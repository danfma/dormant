using Dormant.Abstractions.Querying;

namespace Dormant.Abstractions.Entities;

/// <summary>
/// The non-generic facet of an entity binding, used by the provider-agnostic session for change
/// tracking over type-erased instances (spec FR-014/FR-015). All members dispatch to generated code
/// with no reflection; the implementation casts the <see cref="object"/> arguments to the concrete
/// entity type. The per-tracked-entity snapshot is the only allocation here — it is bookkeeping, never
/// the per-row read path, so it does not violate the no-per-row-boxing budget (SC-004).
/// </summary>
public interface IEntityBinding
{
    /// <summary>Whether the entity carries an optimistic-concurrency token (drives conflict detection).</summary>
    bool TracksConcurrency { get; }

    /// <summary>Captures the current value-column state of <paramref name="entity"/> for later diffing.</summary>
    /// <param name="entity">The entity to snapshot.</param>
    /// <returns>An opaque snapshot consumed by <see cref="Update"/>.</returns>
    object Snapshot(object entity);

    /// <summary>
    /// Builds an UPDATE for the columns that changed since <paramref name="snapshot"/> (changed columns
    /// only, spec FR-014); when a concurrency token is present it is incremented in the SET clause and
    /// matched in the WHERE clause (FR-015). Returns <see langword="null"/> when nothing changed.
    /// </summary>
    /// <param name="entity">The current (possibly mutated) entity.</param>
    /// <param name="snapshot">The snapshot captured when the entity was loaded or last persisted.</param>
    /// <returns>The parameterized UPDATE, or <see langword="null"/> when there is nothing to persist.</returns>
    PreparedStatement? Update(object entity, object snapshot);

    /// <summary>Builds the DELETE-by-primary-key statement (with the concurrency token when present).</summary>
    /// <param name="entity">The entity to delete.</param>
    /// <returns>The parameterized DELETE.</returns>
    PreparedStatement Delete(object entity);
}

/// <summary>
/// The build-time-generated binding between an entity type and the database: no-reflection
/// materialization plus prebuilt SQL. One implementation is emitted per entity and registered into
/// <see cref="EntityBindings"/> at module initialization (research §10). This is how the
/// provider-agnostic session dispatches to generated code without reflection.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IEntityBinding<TEntity> : IEntityBinding
    where TEntity : class
{
    /// <summary>Materializes an entity from the current reader row (value columns in fixed order).</summary>
    /// <param name="reader">The field reader positioned on a row.</param>
    /// <returns>The materialized entity.</returns>
    TEntity Materialize(IFieldReader reader);

    /// <summary>Builds the prebuilt INSERT statement for <paramref name="entity"/> (all mapped columns).</summary>
    /// <param name="entity">The entity to insert.</param>
    /// <returns>The parameterized statement.</returns>
    PreparedStatement Insert(TEntity entity);

    /// <summary>Builds the prebuilt SELECT-by-primary-key statement.</summary>
    /// <param name="key">The primary-key value.</param>
    /// <returns>The parameterized statement.</returns>
    PreparedStatement SelectByKey(object key);
}
