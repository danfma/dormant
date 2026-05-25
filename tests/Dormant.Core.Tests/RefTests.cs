using Dormant.Abstractions.Entities;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Core.Tests;

// Foundational coverage for the typed relationship references (spec FR-009/FR-049 / T012, T100).
public sealed class RefTests
{
    private sealed class Target;

    [Test]
    public async Task Unloaded_ref_is_not_loaded()
    {
        var reference = Ref<Target>.Unloaded;

        await Assert.That(reference.IsLoaded).IsFalse();
        await Assert.That(reference.TryGetLoaded(out _)).IsFalse();
    }

    [Test]
    public async Task Loaded_ref_exposes_value()
    {
        var target = new Target();
        var reference = Ref<Target>.Loaded(target);

        await Assert.That(reference.IsLoaded).IsTrue();
        _ = reference.TryGetLoaded(out var value);
        await Assert.That(value).IsEqualTo(target);
        await Assert.That(reference.Value).IsEqualTo(target);
    }

    [Test]
    public async Task Reading_value_on_unloaded_ref_throws()
    {
        var reference = Ref<Target>.Unloaded;

        await Assert.That(() => reference.Value).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Optional_ref_allows_null_loaded_value()
    {
        var reference = Ref<Target?>.Loaded(null);

        await Assert.That(reference.IsLoaded).IsTrue();
        _ = reference.TryGetLoaded(out var value);
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task Unloaded_set_yields_empty()
    {
        var set = RefSet<Target>.Unloaded;

        await Assert.That(set.IsLoaded).IsFalse();
        _ = set.TryGetLoaded(out var items);
        await Assert.That(items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Loaded_set_exposes_items()
    {
        var target = new Target();
        var set = RefSet<Target>.Loaded([target]);

        await Assert.That(set.IsLoaded).IsTrue();
        _ = set.TryGetLoaded(out var items);
        await Assert.That(items.Count).IsEqualTo(1);
    }
}
