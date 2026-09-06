using Xunit;

namespace HostDeck.IntegrationTests;

/// <summary>
/// Garde-fou de phase 0 : le projet compile, référence bien sa couche et le runner xUnit démarre.
/// Il sera remplacé par les tests réels de la couche dès la phase suivante.
/// </summary>
public sealed class SolutionSmokeTests
{
    [Fact]
    public void ProjectIsWiredAndTestRunnerStarts()
    {
        Assert.True(true);
    }
}
