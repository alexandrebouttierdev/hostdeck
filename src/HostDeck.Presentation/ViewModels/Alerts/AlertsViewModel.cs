using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Alerts;
using HostDeck.Application.Dtos.Alerts;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;

namespace HostDeck.Presentation.ViewModels.Alerts;

/// <summary>Règles d'alerte + éditeur. Sans règle : empty state. Sans sélection : panneau detail vide.</summary>
public partial class AlertsViewModel : PageViewModelBase
{
    private readonly GetAlertRulesUseCase _getRules;
    private readonly CreateAlertRuleUseCase _createRule;
    private readonly UpdateAlertRuleUseCase _updateRule;
    private readonly DeleteAlertRuleUseCase _deleteRule;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private AlertRuleDto? _selectedRule;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private string? _editorDescription;

    [ObservableProperty]
    private MonitoredMetric _editorMetric = MonitoredMetric.CpuUsage;

    [ObservableProperty]
    private Severity _editorSeverity = Severity.High;

    [ObservableProperty]
    private ComparisonOperator _editorComparison = ComparisonOperator.GreaterThan;

    [ObservableProperty]
    private double _editorThresholdValue = 80d;

    [ObservableProperty]
    private int _editorDurationMinutes = 5;

    [ObservableProperty]
    private int _editorCooldownMinutes = 15;

    [ObservableProperty]
    private bool _editorIsEnabled = true;

    public ObservableCollection<AlertRuleDto> Rules { get; } = [];

    public MonitoredMetric[] MetricOptions { get; } = Enum.GetValues<MonitoredMetric>();

    public Severity[] SeverityOptions { get; } = Enum.GetValues<Severity>();

    public ComparisonOperator[] ComparisonOptions { get; } = Enum.GetValues<ComparisonOperator>();

    public AlertsViewModel(
        GetAlertRulesUseCase getRules,
        CreateAlertRuleUseCase createRule,
        UpdateAlertRuleUseCase updateRule,
        DeleteAlertRuleUseCase deleteRule)
    {
        _getRules = getRules;
        _createRule = createRule;
        _updateRule = updateRule;
        _deleteRule = deleteRule;
    }

    public override string Title => "Règles d’alerte";

    public override string Breadcrumb => "Infrastructure › Règles d’alerte";

    public override string StatusSummary =>
        Rules.Count == 0
            ? "0 règle"
            : $"{Rules.Count} règle{(Rules.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedRule is not null;

    public bool IsEditorVisible => IsCreating || HasSelection;

    public bool IsMetricReadOnly => HasSelection && !IsCreating;

    public bool CanDelete => HasSelection && !IsCreating;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _getRules.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        var selectedId = SelectedRule?.RuleId;

        Rules.Clear();
        foreach (var rule in rules)
        {
            Rules.Add(rule);
        }

        IsEmpty = Rules.Count == 0;

        if (!IsCreating)
        {
            SelectedRule = selectedId is Guid id
                ? Rules.FirstOrDefault(rule => rule.RuleId == id)
                : null;
        }

        if (SelectedRule is not null && !IsCreating)
        {
            LoadEditorFromRule(SelectedRule);
        }

        NotifyEditorState();
        OnPropertyChanged(nameof(StatusSummary));
    }

    [RelayCommand]
    private void StartCreate()
    {
        IsCreating = true;
        SelectedRule = null;
        EditorName = string.Empty;
        EditorDescription = null;
        EditorMetric = MonitoredMetric.CpuUsage;
        EditorSeverity = Severity.High;
        EditorComparison = ComparisonOperator.GreaterThan;
        EditorThresholdValue = 80d;
        EditorDurationMinutes = 5;
        EditorCooldownMinutes = 15;
        EditorIsEnabled = true;
        NotifyEditorState();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsCreating = false;
        if (SelectedRule is not null)
        {
            LoadEditorFromRule(SelectedRule);
        }
        else
        {
            ClearEditor();
        }

        NotifyEditorState();
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (IsCreating)
        {
            var created = await _createRule.ExecuteAsync(
                new CreateAlertRuleDto
                {
                    Name = EditorName,
                    Description = EditorDescription,
                    Metric = EditorMetric,
                    Severity = EditorSeverity,
                    Comparison = EditorComparison,
                    ThresholdValue = EditorThresholdValue,
                    Duration = TimeSpan.FromMinutes(EditorDurationMinutes),
                    Cooldown = TimeSpan.FromMinutes(EditorCooldownMinutes),
                    ScopeKind = AlertScopeKind.Global,
                    IsEnabled = EditorIsEnabled,
                },
                cancellationToken).ConfigureAwait(true);

            IsCreating = false;
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            SelectedRule = Rules.FirstOrDefault(rule => rule.RuleId == created.RuleId);
            if (SelectedRule is not null)
            {
                LoadEditorFromRule(SelectedRule);
            }
        }
        else if (SelectedRule is not null)
        {
            await _updateRule.ExecuteAsync(
                new UpdateAlertRuleDto
                {
                    RuleId = SelectedRule.RuleId,
                    Name = EditorName,
                    Description = EditorDescription,
                    Severity = EditorSeverity,
                    Comparison = EditorComparison,
                    ThresholdValue = EditorThresholdValue,
                    Duration = TimeSpan.FromMinutes(EditorDurationMinutes),
                    Cooldown = TimeSpan.FromMinutes(EditorCooldownMinutes),
                    ScopeKind = SelectedRule.ScopeKind,
                    ScopeGroup = SelectedRule.ScopeGroup,
                    ScopeServerId = SelectedRule.ScopeServerId,
                    IsEnabled = EditorIsEnabled,
                },
                cancellationToken).ConfigureAwait(true);

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }

        NotifyEditorState();
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (SelectedRule is null)
        {
            return;
        }

        var ruleId = SelectedRule.RuleId;
        await _deleteRule.ExecuteAsync(ruleId, cancellationToken).ConfigureAwait(true);
        SelectedRule = null;
        ClearEditor();
        IsCreating = false;
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnSelectedRuleChanged(AlertRuleDto? value)
    {
        if (!IsCreating && value is not null)
        {
            LoadEditorFromRule(value);
        }

        NotifyEditorState();
    }

    partial void OnIsCreatingChanged(bool value) => NotifyEditorState();

    private void LoadEditorFromRule(AlertRuleDto rule)
    {
        EditorName = rule.Name;
        EditorDescription = rule.Description;
        EditorMetric = rule.Metric;
        EditorSeverity = rule.Severity;
        EditorComparison = rule.Comparison ?? ComparisonOperator.GreaterThan;
        EditorThresholdValue = rule.ThresholdValue ?? 0d;
        EditorDurationMinutes = Math.Max(1, (int)Math.Round(rule.Duration.TotalMinutes));
        EditorCooldownMinutes = Math.Max(1, (int)Math.Round(rule.Cooldown.TotalMinutes));
        EditorIsEnabled = rule.IsEnabled;
    }

    private void ClearEditor()
    {
        EditorName = string.Empty;
        EditorDescription = null;
        EditorMetric = MonitoredMetric.CpuUsage;
        EditorSeverity = Severity.High;
        EditorComparison = ComparisonOperator.GreaterThan;
        EditorThresholdValue = 80d;
        EditorDurationMinutes = 5;
        EditorCooldownMinutes = 15;
        EditorIsEnabled = true;
    }

    private void NotifyEditorState()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsEditorVisible));
        OnPropertyChanged(nameof(IsMetricReadOnly));
        OnPropertyChanged(nameof(CanDelete));
        DeleteCommand.NotifyCanExecuteChanged();
    }
}
