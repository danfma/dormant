using System;
using System.Collections;
using System.Collections.Generic;

namespace Dormant.SourceGeneration.Parsing;

/// <summary>
/// A small immutable array wrapper with value (element-wise) equality, so incremental-generator
/// pipeline models stay cacheable and produce deterministic output (research §5). Unlike
/// <see cref="System.Collections.Immutable.ImmutableArray{T}"/>, equality compares contents, not the
/// underlying reference.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items;

    public EquatableArray(T[]? items) => _items = items;

    public T this[int index] => (_items ?? [])[index];

    public int Count => _items?.Length ?? 0;

    public bool Equals(EquatableArray<T> other)
    {
        var a = _items ?? [];
        var b = other._items ?? [];
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in _items ?? [])
            {
                hash = (hash * 31) + EqualityComparer<T>.Default.GetHashCode(item!);
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in _items ?? [])
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) =>
        left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) =>
        !left.Equals(right);
}
