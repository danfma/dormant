namespace Dormant.Abstractions.Entities;

/// <summary>
/// A relationship to many related entities with **set** semantics (unordered, no duplicates), carrying
/// explicit loaded/unloaded state (spec FR-009/FR-049). The default value is <see cref="Unloaded"/>,
/// never an empty collection, so "not loaded" stays distinguishable from "loaded but empty".
/// </summary>
/// <typeparam name="T">The related entity type.</typeparam>
public readonly struct RefSet<T>
    where T : class
{
    private readonly IReadOnlyList<T>? _items;

    private RefSet(bool isLoaded, IReadOnlyList<T>? items)
    {
        IsLoaded = isLoaded;
        _items = items;
    }

    /// <summary>Gets a value indicating whether the related entities have been loaded.</summary>
    public bool IsLoaded { get; }

    /// <summary>The unloaded set.</summary>
    public static RefSet<T> Unloaded => default;

    /// <summary>Creates a loaded set wrapping <paramref name="items"/>.</summary>
    /// <param name="items">The loaded related entities (may be empty).</param>
    /// <returns>A loaded <see cref="RefSet{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static RefSet<T> Loaded(IReadOnlyList<T> items) =>
        new(isLoaded: true, items ?? throw new ArgumentNullException(nameof(items)));

    /// <summary>Attempts to read the loaded items; the unloaded case must be handled by the caller.</summary>
    /// <param name="items">The loaded items (empty when unloaded) when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if loaded; otherwise <see langword="false"/>.</returns>
    public bool TryGetLoaded(out IReadOnlyList<T> items)
    {
        items = _items ?? [];
        return IsLoaded;
    }
}
