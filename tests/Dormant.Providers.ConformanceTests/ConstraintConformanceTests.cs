using System;
using System.Threading.Tasks;
using Dormant.Providers.ConformanceTests.Schema.Catalog;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Providers.ConformanceTests;

/// <summary>
/// Feature 012 (SC-002): the constraints declared in the schema are enforced by the real database.
/// The generated CREATE TABLE carries them; violating writes must be rejected on both providers.
/// </summary>
public sealed class ConstraintConformanceTests
{
    private static async Task<bool> Throws(Func<Task> action)
    {
        try
        {
            await action();
            return false;
        }
        catch
        {
            return true;
        }
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Valid_row_is_accepted(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        await using var session = await harness.Factory.OpenSessionAsync();

        await session.CreateAccount(Guid.NewGuid(), "ok");
        await session.CommitAsync();
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Max_length_check_rejects_over_long_value(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);
        await using var session = await harness.Factory.OpenSessionAsync();

        // email max_length(10) → a longer value must be rejected by the CHECK constraint.
        var rejected = await Throws(async () =>
        {
            await session.CreateAccount(Guid.NewGuid(), "this-is-way-too-long");
            await session.CommitAsync();
        });

        await Assert.That(rejected).IsTrue();
    }

    [Test]
    [Arguments("postgres")]
    [Arguments("sqlite")]
    public async Task Unique_constraint_rejects_duplicate(string provider)
    {
        await using var harness = await ProviderHarness.CreateAsync(provider);

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            await session.CreateAccount(Guid.NewGuid(), "dup");
            await session.CommitAsync();
        }

        await using (var session = await harness.Factory.OpenSessionAsync())
        {
            var rejected = await Throws(async () =>
            {
                await session.CreateAccount(Guid.NewGuid(), "dup");
                await session.CommitAsync();
            });

            await Assert.That(rejected).IsTrue();
        }
    }
}
