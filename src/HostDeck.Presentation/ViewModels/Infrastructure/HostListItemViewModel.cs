using System.Collections.Generic;
using System.Globalization;
using HostDeck.Application.Dtos.Servers;

namespace HostDeck.Presentation.ViewModels.Infrastructure;

/// <summary>
/// Ligne d'inventaire : résumé serveur + séries sparkline optionnelles.
/// Sans historique réel, les séries restent absentes (jamais inventées).
/// </summary>
public sealed class HostListItemViewModel
{
    public HostListItemViewModel(
        ServerSummaryDto server,
        IReadOnlyList<float>? cpuSeries = null,
        IReadOnlyList<float>? memorySeries = null,
        IReadOnlyList<float>? diskSeries = null,
        IReadOnlyList<float>? networkSeries = null)
    {
        Server = server;
        CpuSeries = cpuSeries;
        MemorySeries = memorySeries;
        DiskSeries = diskSeries;
        NetworkSeries = networkSeries;
    }

    public ServerSummaryDto Server { get; }

    public IReadOnlyList<float>? CpuSeries { get; }

    public IReadOnlyList<float>? MemorySeries { get; }

    public IReadOnlyList<float>? DiskSeries { get; }

    public IReadOnlyList<float>? NetworkSeries { get; }

    public bool HasCpuSeries => CpuSeries is { Count: > 1 };

    public bool HasMemorySeries => MemorySeries is { Count: > 1 };

    public bool HasDiskSeries => DiskSeries is { Count: > 1 };

    public bool HasNetworkSeries => NetworkSeries is { Count: > 1 };

    public string Name => Server.Name;

    public string Address => Server.Address;

    public string Status => Server.Status.ToString();

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
