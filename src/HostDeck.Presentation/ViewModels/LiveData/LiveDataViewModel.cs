using CommunityToolkit.Mvvm.ComponentModel;

namespace HostDeck.Presentation.ViewModels.LiveData;

/// <summary>Métriques temps réel. Sans flotte / sans sélection : empty state.</summary>
public partial class LiveDataViewModel : PageViewModelBase
{
    [ObservableProperty]
    private bool _isEmpty = true;

    public override string Title => "Données en direct";

    public override string Breadcrumb => "Supervision › Données en direct";

    public override string StatusSummary => "Aucune série";
}
