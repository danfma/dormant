namespace Dormant.Abstractions.Entities;

/// <summary>
/// A relationship to many related entities with **bag** semantics (unordered, duplicates allowed),
/// carrying explicit loaded/unloaded state (spec FR-009/FR-049). Default is <see cref="Unloaded"/>,
/// never empty.
/// </summary>
/// <typeparam name="T">The related entity type.</typeparam>
public readonly struct RefBag<T>
    where T : class
{
    private readonly IReadOnlyList<T>? _items;

    private RefBag(bool isLoaded, IReadOnlyList<T>? items)
    {
        IsLoaded = isLoaded;
        _items = items;
    }

    /// <summary>Gets a value indicating whether the related entities have been loaded.</summary>
    public bool IsLoaded { get; }

    /// <summary>The unloaded bag.</summary>
    public static RefBag<T> Unloaded => default;

    /// <summary>Creates a loaded bag wrapping <paramref name="items"/> (duplicates allowed).</summary>
    /// <param name="items">The loaded related entities.</param>
    /// <returns>A loaded <see cref="RefBag{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static RefBag<T> Loaded(IReadOnlyList<T> items) =>
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
