namespace Dormant.Abstractions.Entities;

/// <summary>
/// A relationship to many related entities with **map** semantics (keyed by <typeparamref name="TKey"/>),
/// carrying explicit loaded/unloaded state (spec FR-009/FR-049). Default is <see cref="Unloaded"/>,
/// never empty.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The related entity (value) type.</typeparam>
public readonly struct RefMap<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly IReadOnlyDictionary<TKey, TValue>? _entries;

    private RefMap(bool isLoaded, IReadOnlyDictionary<TKey, TValue>? entries)
    {
        IsLoaded = isLoaded;
        _entries = entries;
    }

    /// <summary>Gets a value indicating whether the related entries have been loaded.</summary>
    public bool IsLoaded { get; }

    /// <summary>The unloaded map.</summary>
    public static RefMap<TKey, TValue> Unloaded => default;

    /// <summary>Creates a loaded map wrapping <paramref name="entries"/>.</summary>
    /// <param name="entries">The loaded key→entity entries.</param>
    /// <returns>A loaded <see cref="RefMap{TKey, TValue}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    public static RefMap<TKey, TValue> Loaded(IReadOnlyDictionary<TKey, TValue> entries) =>
        new(isLoaded: true, entries ?? throw new ArgumentNullException(nameof(entries)));

    /// <summary>Attempts to read the loaded entries; the unloaded case must be handled by the caller.</summary>
    /// <param name="entries">The loaded entries (empty when unloaded) when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if loaded; otherwise <see langword="false"/>.</returns>
    public bool TryGetLoaded(out IReadOnlyDictionary<TKey, TValue> entries)
    {
        entries = _entries ?? System.Collections.ObjectModel.ReadOnlyDictionary<TKey, TValue>.Empty;
        return IsLoaded;
    }
}
