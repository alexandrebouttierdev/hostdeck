using System.Globalization;
using HostDeck.Application.Dtos.Docker;
using HostDeck.Domain.Docker;

namespace HostDeck.Presentation.ViewModels.Docker;

/// <summary>Ligne affichée dans la grille Docker.</summary>
public sealed class DockerContainerItemViewModel
{
    public DockerContainerItemViewModel(DockerContainerSummaryDto model)
    {
        Model = model;
    }

    public DockerContainerSummaryDto Model { get; }

    public string Name => Model.Name;

    public string Image => Model.Image;

    public string StatusText => MapStatus(Model.Status);

    public string HealthText => MapHealth(Model.Health);

    public string CpuText =>
        Model.Stats is { } stats
            ? stats.CpuPercent.ToString("0.#", CultureInfo.InvariantCulture) + " %"
            : "—";

    public string MemoryText =>
        Model.Stats is { } stats
            ? stats.MemoryPercent.ToString("0.#", CultureInfo.InvariantCulture) + " %"
            : "—";

    public bool CanStart =>
        Model.Status is DockerContainerStatus.Exited
            or DockerContainerStatus.Created
            or DockerContainerStatus.Dead;

    public bool CanStop =>
        Model.Status is DockerContainerStatus.Running
            or DockerContainerStatus.Restarting
            or DockerContainerStatus.Paused;

    public bool CanRestart => Model.Status != DockerContainerStatus.Removing;

    private static string MapStatus(DockerContainerStatus status) =>
        status switch
        {
            DockerContainerStatus.Created => "Créé",
            DockerContainerStatus.Running => "En cours",
            DockerContainerStatus.Paused => "En pause",
            DockerContainerStatus.Restarting => "Redémarrage",
            DockerContainerStatus.Removing => "Suppression",
            DockerContainerStatus.Exited => "Arrêté",
            DockerContainerStatus.Dead => "Mort",
            _ => "Inconnu",
        };

    private static string MapHealth(DockerHealthStatus health) =>
        health switch
        {
            DockerHealthStatus.None => "—",
            DockerHealthStatus.Starting => "Démarrage",
            DockerHealthStatus.Healthy => "Sain",
            DockerHealthStatus.Unhealthy => "Non sain",
            _ => "—",
        };
}
