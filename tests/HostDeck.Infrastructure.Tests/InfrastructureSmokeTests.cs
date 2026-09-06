using Xunit;

namespace HostDeck.Infrastructure.Tests;

/// <summary>
/// Garde-fou de phase 0 : le projet compile, référence bien sa couche et le runner xUnit démarre.
/// Il sera remplacé par les tests réels de la couche dès la phase suivante.
/// </summary>
public sealed class InfrastructureSmokeTests
{
    [Fact]
    public void ProjectIsWiredAndTestRunnerStarts()
    {
        Assert.True(true);
    }
}
