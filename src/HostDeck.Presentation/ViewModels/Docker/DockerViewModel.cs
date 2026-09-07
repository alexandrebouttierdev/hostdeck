namespace HostDeck.Presentation.ViewModels.Docker;

/// <summary>
/// Écran Docker du rail. La collecte Docker n'est pas branchée ici : empty state intentionnel.
/// </summary>
public sealed class DockerViewModel : PageViewModelBase
{
    public override string Title => "Docker";

    public override string Breadcrumb => "Infrastructure › Docker";

    public override string StatusSummary => "0 conteneur";
}
