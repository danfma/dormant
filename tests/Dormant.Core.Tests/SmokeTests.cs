using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Dormant.Core.Tests;

// Placeholder smoke test confirming the TUnit harness runs. Replaced by real US1+ tests.
public sealed class SmokeTests
{
    [Test]
    public async Task Harness_runs()
    {
        await Assert.That(Environment.Version.Major).IsGreaterThanOrEqualTo(10);
    }
}
