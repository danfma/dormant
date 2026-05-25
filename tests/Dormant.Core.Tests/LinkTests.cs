using Dormant.Abstractions.Links;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Core.Tests;

// Foundational coverage for the typed link load-state (spec FR-009 / T012).
public sealed class LinkTests
{
    private sealed class Target;

    [Test]
    public async Task Unloaded_link_is_not_loaded()
    {
        var link = Link<Target>.Unloaded;

        await Assert.That(link.IsLoaded).IsFalse();
        await Assert.That(link.TryGetLoaded(out _)).IsFalse();
    }

    [Test]
    public async Task Loaded_link_exposes_value()
    {
        var target = new Target();
        var link = Link<Target>.Loaded(target);

        await Assert.That(link.IsLoaded).IsTrue();
        _ = link.TryGetLoaded(out var value);
        await Assert.That(value).IsEqualTo(target);
        await Assert.That(link.Value).IsEqualTo(target);
    }

    [Test]
    public async Task Reading_value_on_unloaded_link_throws()
    {
        var link = Link<Target>.Unloaded;

        await Assert.That(() => link.Value).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Unloaded_link_set_yields_empty()
    {
        var set = LinkSet<Target>.Unloaded;

        await Assert.That(set.IsLoaded).IsFalse();
        _ = set.TryGetLoaded(out var items);
        await Assert.That(items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Loaded_link_set_exposes_items()
    {
        var target = new Target();
        var set = LinkSet<Target>.Loaded([target]);

        await Assert.That(set.IsLoaded).IsTrue();
        _ = set.TryGetLoaded(out var items);
        await Assert.That(items.Count).IsEqualTo(1);
    }
}
