namespace Dormant.Abstractions.Entities;

/// <summary>
/// A reference to a single related entity on a full entity, carrying explicit loaded/unloaded state so
/// unfetched related data cannot be read as if present (spec FR-009). Optionality is the nullability of
/// <typeparamref name="T"/> (orthogonal to load-state): <c>Ref&lt;User&gt;</c> is required (loaded value
/// non-null); <c>Ref&lt;User?&gt;</c> is optional (loaded value may be <see langword="null"/>).
/// </summary>
/// <typeparam name="T">The related entity type (use <c>T?</c> for an optional relationship).</typeparam>
public readonly struct Ref<T>
    where T : class?
{
    private readonly T _value;

    private Ref(bool isLoaded, T value)
    {
        IsLoaded = isLoaded;
        _value = value;
    }

    /// <summary>Gets a value indicating whether the related entity has been loaded.</summary>
    public bool IsLoaded { get; }

    /// <summary>The unloaded reference. Reading <see cref="Value"/> on it throws.</summary>
    public static Ref<T> Unloaded => default;

    /// <summary>Creates a loaded reference wrapping <paramref name="value"/>.</summary>
    /// <param name="value">The loaded related entity (may be <see langword="null"/> when <typeparamref name="T"/> is nullable).</param>
    /// <returns>A loaded <see cref="Ref{T}"/>.</returns>
    public static Ref<T> Loaded(T value) => new(isLoaded: true, value);

    /// <summary>Attempts to read the loaded value; the unloaded case must be handled by the caller.</summary>
    /// <param name="value">The loaded value when this returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if loaded; otherwise <see langword="false"/>.</returns>
    public bool TryGetLoaded(out T? value)
    {
        value = _value;
        return IsLoaded;
    }

    /// <summary>Gets the loaded value. Prefer <see cref="TryGetLoaded"/>; this throws when unloaded.</summary>
    /// <exception cref="InvalidOperationException">The reference is unloaded.</exception>
    public T Value =>
        IsLoaded
            ? _value
            : throw new InvalidOperationException(
                "Ref is unloaded; handle the unloaded case (e.g. ISession.LoadAsync) before reading Value."
            );
}
