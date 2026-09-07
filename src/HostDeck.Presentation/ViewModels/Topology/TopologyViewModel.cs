namespace HostDeck.Presentation.ViewModels.Topology;

/// <summary>Carte de topologie. Sans hôte : empty state explicite.</summary>
public sealed class TopologyViewModel : PageViewModelBase
{
    public override string Title => "Topologie";

    public override string Breadcrumb => "Infrastructure › Topologie";

    public override string StatusSummary => "Carte vide";
}
