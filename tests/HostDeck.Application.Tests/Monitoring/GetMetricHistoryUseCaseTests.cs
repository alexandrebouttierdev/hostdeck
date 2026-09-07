using System;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Tests.Fakes;
using Xunit;

namespace HostDeck.Application.Tests.Monitoring;

public sealed class GetMetricHistoryUseCaseTests
{
    [Fact]
    public async Task ReturnsRepositoryHistoryWithoutInventingPoints()
    {
        var metrics = new FakeMetricsRepository();
        var useCase = new GetMetricHistoryUseCase(metrics);
        var request = new MetricHistoryRequestDto
        {
            ServerId = Guid.NewGuid(),
            From = DateTimeOffset.UtcNow.AddHours(-1),
            To = DateTimeOffset.UtcNow,
            Series = [MetricSeriesKind.CpuTotal],
            MaxPoints = 60,
        };

        var history = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(request.ServerId, history.ServerId);
        Assert.Empty(history.Series);
    }
}
