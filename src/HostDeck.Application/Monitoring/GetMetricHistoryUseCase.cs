using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Ports;

namespace HostDeck.Application.Monitoring;

/// <summary>
/// Renvoie un historique déjà agrégé pour un graphique ou une sparkline.
/// </summary>
public sealed class GetMetricHistoryUseCase
{
    private readonly IMetricsRepository _metrics;

    public GetMetricHistoryUseCase(IMetricsRepository metrics)
    {
        _metrics = metrics;
    }

    public Task<MetricHistoryDto> ExecuteAsync(
        MetricHistoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _metrics.GetHistoryAsync(request, cancellationToken);
    }
}
