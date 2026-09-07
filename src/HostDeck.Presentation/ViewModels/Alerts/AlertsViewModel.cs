using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Alerts;
using HostDeck.Application.Dtos.Alerts;

namespace HostDeck.Presentation.ViewModels.Alerts;

/// <summary>Règles d'alerte + éditeur. Sans règle : empty state. Sans sélection : panneau detail vide.</summary>
public partial class AlertsViewModel : PageViewModelBase
{
    private readonly GetAlertRulesUseCase _getRules;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private AlertRuleDto? _selectedRule;

    public ObservableCollection<AlertRuleDto> Rules { get; } = [];

    public AlertsViewModel(GetAlertRulesUseCase getRules)
    {
        _getRules = getRules;
    }

    public override string Title => "Règles d’alerte";

    public override string Breadcrumb => "Infrastructure › Règles d’alerte";

    public override string StatusSummary =>
        Rules.Count == 0
            ? "0 règle"
            : $"{Rules.Count} règle{(Rules.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedRule is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _getRules.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Rules.Clear();
        foreach (var rule in rules)
        {
            Rules.Add(rule);
        }

        IsEmpty = Rules.Count == 0;
        if (IsEmpty)
        {
            SelectedRule = null;
        }

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnSelectedRuleChanged(AlertRuleDto? value)
        => OnPropertyChanged(nameof(HasSelection));
}
