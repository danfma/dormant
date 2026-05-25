namespace Dormant.Abstractions.Links;

/// <summary>
/// A relationship to many related entities on a full entity, carrying explicit loaded/unloaded state
/// so unfetched related data cannot be read as if present (spec FR-009).
/// </summary>
/// <typeparam name="T">The related entity type.</typeparam>
public readonly struct LinkSet<T>
    where T : class
{
    private readonly IReadOnlyList<T>? _items;

    private LinkSet(bool isLoaded, IReadOnlyList<T>? items)
    {
        IsLoaded = isLoaded;
        _items = items;
    }

    /// <summary>Gets a value indicating whether the related entities have been loaded.</summary>
    public bool IsLoaded { get; }

    /// <summary>The unloaded link set.</summary>
    public static LinkSet<T> Unloaded => default;

    /// <summary>Creates a loaded link set wrapping <paramref name="items"/>.</summary>
    /// <param name="items">The loaded related entities (may be empty).</param>
    /// <returns>A loaded <see cref="LinkSet{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    public static LinkSet<T> Loaded(IReadOnlyList<T> items) =>
        new(isLoaded: true, items ?? throw new ArgumentNullException(nameof(items)));

    /// <summary>Attempts to read the loaded items; the unloaded case must be handled by the caller.</summary>
    /// <param name="items">The loaded items (empty when unloaded) when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the link set is loaded; otherwise <see langword="false"/>.</returns>
    public bool TryGetLoaded(out IReadOnlyList<T> items)
    {
        items = _items ?? [];
        return IsLoaded;
    }
}
