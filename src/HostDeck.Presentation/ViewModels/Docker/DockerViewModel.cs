using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Docker;
using HostDeck.Application.Errors;

namespace HostDeck.Presentation.ViewModels.Docker;

/// <summary>Conteneurs Docker de la flotte. Liste vide → empty state, sans données inventées.</summary>
public partial class DockerViewModel : PageViewModelBase
{
    private readonly ListDockerContainersUseCase _listContainers;
    private readonly StartDockerContainerUseCase _startContainer;
    private readonly StopDockerContainerUseCase _stopContainer;
    private readonly RestartDockerContainerUseCase _restartContainer;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private DockerContainerItemViewModel? _selectedContainer;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<DockerContainerItemViewModel> Containers { get; } = [];

    public DockerViewModel(
        ListDockerContainersUseCase listContainers,
        StartDockerContainerUseCase startContainer,
        StopDockerContainerUseCase stopContainer,
        RestartDockerContainerUseCase restartContainer)
    {
        _listContainers = listContainers;
        _startContainer = startContainer;
        _stopContainer = stopContainer;
        _restartContainer = restartContainer;
    }

    public override string Title => "Docker";

    public override string Breadcrumb => "Infrastructure › Docker";

    public override string StatusSummary =>
        Containers.Count == 0
            ? "0 conteneur"
            : $"{Containers.Count} conteneur{(Containers.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedContainer is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;

        try
        {
            var items = await _listContainers.ExecuteAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(true);

            var previousId = SelectedContainer?.Model.ContainerId;
            Containers.Clear();

            DockerContainerItemViewModel? restored = null;
            foreach (var item in items)
            {
                var row = new DockerContainerItemViewModel(item);
                Containers.Add(row);
                if (previousId is not null && item.ContainerId == previousId)
                {
                    restored = row;
                }
            }

            IsEmpty = Containers.Count == 0;
            SelectedContainer = IsEmpty ? null : restored ?? SelectedContainer;
        }
        catch (HostDeckException exception)
        {
            ErrorMessage = exception.UserMessage;
            Containers.Clear();
            IsEmpty = true;
            SelectedContainer = null;
        }

        NotifyActionCommands();
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand(CanExecute = nameof(CanStartSelected))]
    private async Task StartSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedContainer is null)
        {
            return;
        }

        await RunActionAsync(
            () => _startContainer.ExecuteAsync(
                SelectedContainer.Model.ServerId,
                SelectedContainer.Model.ContainerId,
                cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanStopSelected))]
    private async Task StopSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedContainer is null)
        {
            return;
        }

        await RunActionAsync(
            () => _stopContainer.ExecuteAsync(
                SelectedContainer.Model.ServerId,
                SelectedContainer.Model.ContainerId,
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRestartSelected))]
    private async Task RestartSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedContainer is null)
        {
            return;
        }

        await RunActionAsync(
            () => _restartContainer.ExecuteAsync(
                SelectedContainer.Model.ServerId,
                SelectedContainer.Model.ContainerId,
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartContainerAsync(
        DockerContainerItemViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await RunActionAsync(
            () => _startContainer.ExecuteAsync(
                item.Model.ServerId,
                item.Model.ContainerId,
                cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StopContainerAsync(
        DockerContainerItemViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await RunActionAsync(
            () => _stopContainer.ExecuteAsync(
                item.Model.ServerId,
                item.Model.ContainerId,
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RestartContainerAsync(
        DockerContainerItemViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await RunActionAsync(
            () => _restartContainer.ExecuteAsync(
                item.Model.ServerId,
                item.Model.ContainerId,
                cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(true);
    }

    private bool CanStartSelected() => SelectedContainer?.CanStart == true;

    private bool CanStopSelected() => SelectedContainer?.CanStop == true;

    private bool CanRestartSelected() => SelectedContainer?.CanRestart == true;

    partial void OnSelectedContainerChanged(DockerContainerItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        NotifyActionCommands();
    }

    private async Task RunActionAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        try
        {
            await action().ConfigureAwait(true);
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (HostDeckException exception)
        {
            ErrorMessage = exception.UserMessage;
        }
    }

    private void NotifyActionCommands()
    {
        StartSelectedCommand.NotifyCanExecuteChanged();
        StopSelectedCommand.NotifyCanExecuteChanged();
        RestartSelectedCommand.NotifyCanExecuteChanged();
    }
}
