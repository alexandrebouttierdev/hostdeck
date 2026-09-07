using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Charts;

namespace HostDeck.Presentation.ViewModels.Infrastructure;

/// <summary>
/// Ligne d'inventaire : résumé serveur + séries sparkline optionnelles.
/// Sans historique réel, les séries restent absentes (jamais inventées).
/// </summary>
public partial class HostListItemViewModel : ObservableObject
{
    public const int SparklineCapacity = 60;

    [ObservableProperty]
    private ServerSummaryDto _server;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCpuSeries))]
    private IReadOnlyList<float>? _cpuSeries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMemorySeries))]
    private IReadOnlyList<float>? _memorySeries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiskSeries))]
    private IReadOnlyList<float>? _diskSeries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNetworkSeries))]
    private IReadOnlyList<float>? _networkSeries;

    public HostListItemViewModel(
        ServerSummaryDto server,
        IReadOnlyList<float>? cpuSeries = null,
        IReadOnlyList<float>? memorySeries = null,
        IReadOnlyList<float>? diskSeries = null,
        IReadOnlyList<float>? networkSeries = null)
    {
        _server = server;
        _cpuSeries = cpuSeries;
        _memorySeries = memorySeries;
        _diskSeries = diskSeries;
        _networkSeries = networkSeries;
    }

    public bool HasCpuSeries => CpuSeries is { Count: > 1 };

    public bool HasMemorySeries => MemorySeries is { Count: > 1 };

    public bool HasDiskSeries => DiskSeries is { Count: > 1 };

    public bool HasNetworkSeries => NetworkSeries is { Count: > 1 };

    public string Name => Server.Name;

    public string Address => Server.Address;

    public string Status => FormatStatus(Server.Status);

    public string Group => Server.Group ?? "—";

    public string OperatingSystem => Server.OperatingSystem ?? "—";

    public string CpuText => FormatPercent(Server.Latest?.CpuPercent);

    public string MemoryText => FormatPercent(Server.Latest?.MemoryPercent);

    public string DiskText => FormatPercent(Server.Latest?.DiskPercent);

    public string LoadText =>
        Server.Latest is null
            ? "—"
            : Server.Latest.LoadOneMinute.ToString("0.00", CultureInfo.CurrentCulture);

    public string UptimeText =>
        Server.Latest is null ? "—" : FormatUptime(Server.Latest.Uptime);

    public void ApplyMetric(LatestMetricDto metric)
    {
        Server = Server with
        {
            Latest = metric,
            LastCollectedAt = metric.ObservedAt,
            Status = ServerStatus.Online,
        };

        CpuSeries = MetricSeriesMapper.AppendBounded(CpuSeries, (float)metric.CpuPercent, SparklineCapacity);
        MemorySeries = MetricSeriesMapper.AppendBounded(MemorySeries, (float)metric.MemoryPercent, SparklineCapacity);
        DiskSeries = MetricSeriesMapper.AppendBounded(DiskSeries, (float)metric.DiskPercent, SparklineCapacity);
        NetworkSeries = MetricSeriesMapper.AppendBounded(
            NetworkSeries,
            (float)metric.NetworkReceivedBytesPerSecond,
            SparklineCapacity);

        NotifyMetricsChanged();
    }

    public void ApplyStatus(ServerStatus status)
    {
        Server = Server with { Status = status };
        OnPropertyChanged(nameof(Status));
    }

    private void NotifyMetricsChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Address));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Group));
        OnPropertyChanged(nameof(OperatingSystem));
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(DiskText));
        OnPropertyChanged(nameof(LoadText));
        OnPropertyChanged(nameof(UptimeText));
    }

    private static string FormatStatus(ServerStatus status) => status switch
    {
        ServerStatus.Online => "En ligne",
        ServerStatus.Offline => "Hors ligne",
        ServerStatus.GatewayUnavailable => "Bastion indisponible",
        ServerStatus.AuthenticationFailed => "Authentification refusée",
        ServerStatus.HostKeyRejected => "Clé d'hôte refusée",
        _ => "Inconnu",
    };

    private static string FormatPercent(double? value)
        => value is null ? "—" : string.Create(CultureInfo.CurrentCulture, $"{value.Value:0}%");

    private static string FormatUptime(System.TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return string.Create(
                CultureInfo.CurrentCulture,
                $"{(int)uptime.TotalDays}j {uptime.Hours}h");
        }

        if (uptime.TotalHours >= 1)
        {
            return string.Create(CultureInfo.CurrentCulture, $"{(int)uptime.TotalHours}h {uptime.Minutes}m");
        }

        return string.Create(CultureInfo.CurrentCulture, $"{(int)uptime.TotalMinutes}m");
    }
}
