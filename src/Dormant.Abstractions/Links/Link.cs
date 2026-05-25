namespace Dormant.Abstractions.Links;

/// <summary>
/// A relationship to a single related entity on a full entity, carrying explicit loaded/unloaded
/// state so unfetched related data cannot be read as if present (spec FR-009).
/// </summary>
/// <typeparam name="T">The related entity type.</typeparam>
public readonly struct Link<T>
    where T : class
{
    private readonly T? _value;

    private Link(bool isLoaded, T? value)
    {
        IsLoaded = isLoaded;
        _value = value;
    }

    /// <summary>Gets a value indicating whether the related entity has been loaded.</summary>
    public bool IsLoaded { get; }

    /// <summary>The unloaded link. Reading <see cref="Value"/> on it throws.</summary>
    public static Link<T> Unloaded => default;

    /// <summary>Creates a loaded link wrapping <paramref name="value"/> (which may be <see langword="null"/>
    /// when the optional relationship has no target).</summary>
    /// <param name="value">The loaded related entity, or <see langword="null"/>.</param>
    /// <returns>A loaded <see cref="Link{T}"/>.</returns>
    public static Link<T> Loaded(T? value) => new(isLoaded: true, value);

    /// <summary>Attempts to read the loaded value; the unloaded case must be handled by the caller.</summary>
    /// <param name="value">The loaded value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the link is loaded; otherwise <see langword="false"/>.</returns>
    public bool TryGetLoaded(out T? value)
    {
        value = _value;
        return IsLoaded;
    }

    /// <summary>Gets the loaded value. Prefer <see cref="TryGetLoaded"/>; this throws when unloaded.</summary>
    /// <exception cref="InvalidOperationException">The link is unloaded.</exception>
    public T? Value => IsLoaded
        ? _value
        : throw new InvalidOperationException(
            "Link is unloaded; handle the unloaded case (e.g. ISession.LoadAsync) before reading Value.");
}
