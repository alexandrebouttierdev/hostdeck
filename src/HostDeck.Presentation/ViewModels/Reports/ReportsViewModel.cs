namespace HostDeck.Presentation.ViewModels.Reports;

/// <summary>Rapports V1 : layout fidèle, contenu en empty state tant que l'historique est absent.</summary>
public sealed class ReportsViewModel : PageViewModelBase
{
    public override string Title => "Rapports";

    public override string Breadcrumb => "Analyse › Rapports";

    public override string StatusSummary => "Aucun rapport";
}
