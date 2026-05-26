using Dormant.Providers.ConformanceTests.Schema.Catalog;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Providers.ConformanceTests;

/// <summary>
/// SQLite-specific behavior (no Docker): JSON-as-TEXT affinity round-trip, and per-store in-memory isolation
/// (each harness is a distinct shared in-memory database — 005 D12 clean-store-per-case).
/// </summary>
public sealed class SqliteSpecificTests
{
    [Test]
    public async Task Json_value_round_trips_as_text()
    {
        await using var harness = await ProviderHarness.CreateAsync("sqlite");
        var id = Guid.NewGuid();
        const string json = "{\"k\":1,\"items\":[\"a\",\"b\"]}";

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateDocument(id, json);
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var document = await session.GetAsync<Document>(id);
            await Assert.That(document).IsNotNull();
            await Assert.That(document!.Data).IsEqualTo(json);
        }
    }

    [Test]
    public async Task In_memory_stores_are_isolated_per_harness()
    {
        await using var first = await ProviderHarness.CreateAsync("sqlite");
        await using var second = await ProviderHarness.CreateAsync("sqlite");
        var id = Guid.NewGuid();

        await using (var session = await first.Factory.OpenSessionAsync())
        {
            await session.CreateWidget(id, "only-in-first", 1);
            await session.CommitAsync();
        }

        await using (var session = await second.Factory.OpenSessionAsync())
        {
            var widget = await session.GetAsync<Widget>(id);
            await Assert.That(widget).IsNull();
        }
    }
}
