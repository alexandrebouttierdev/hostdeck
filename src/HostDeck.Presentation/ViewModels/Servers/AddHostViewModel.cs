using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Servers;
using HostDeck.Domain.Servers;

namespace HostDeck.Presentation.ViewModels.Servers;

/// <summary>
/// Formulaire d'ajout d'hôte (FR) : saisie, test SSH, approbation de clé, enregistrement.
/// </summary>
public partial class AddHostViewModel : ObservableObject
{
    private readonly AddServerUseCase _addServer;
    private readonly TestConnectionUseCase _testConnection;
    private readonly ApproveHostKeyUseCase _approveHostKey;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = "root";

    [ObservableProperty]
    private CredentialKind _credentialKind = CredentialKind.PrivateKey;

    [ObservableProperty]
    private string _secret = string.Empty;

    [ObservableProperty]
    private string? _passphrase;

    [ObservableProperty]
    private int _monitoringIntervalSeconds = 60;

    [ObservableProperty]
    private bool _dockerEnabled;

    [ObservableProperty]
    private string? _group;

    [ObservableProperty]
    private bool _useJumpHost;

    [ObservableProperty]
    private string _jumpAddress = string.Empty;

    [ObservableProperty]
    private int _jumpPort = 22;

    [ObservableProperty]
    private string _jumpUsername = string.Empty;

    [ObservableProperty]
    private string _jumpCredentialKey = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isSecurityWarning;

    [ObservableProperty]
    private string? _presentedFingerprint;

    [ObservableProperty]
    private string? _knownFingerprint;

    [ObservableProperty]
    private bool _canApproveHostKey;

    [ObservableProperty]
    private bool _connectionSucceeded;

    /// <summary>Branché par le dialogue pour fermer la fenêtre sans connaître <c>Window</c>.</summary>
    public Func<bool, Task>? CloseHandler { get; set; }

    public AddHostViewModel(
        AddServerUseCase addServer,
        TestConnectionUseCase testConnection,
        ApproveHostKeyUseCase approveHostKey)
    {
        _addServer = addServer;
        _testConnection = testConnection;
        _approveHostKey = approveHostKey;
    }

    public bool IsPasswordAuth => CredentialKind == CredentialKind.Password;

    public bool IsPrivateKeyAuth => CredentialKind == CredentialKind.PrivateKey;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasPresentedFingerprint => !string.IsNullOrWhiteSpace(PresentedFingerprint);

    public bool HasKnownFingerprint => !string.IsNullOrWhiteSpace(KnownFingerprint);

    public int AuthModeIndex
    {
        get => CredentialKind == CredentialKind.Password ? 1 : 0;
        set
        {
            CredentialKind = value == 1 ? CredentialKind.Password : CredentialKind.PrivateKey;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPasswordAuth));
            OnPropertyChanged(nameof(IsPrivateKeyAuth));
        }
    }

    partial void OnCredentialKindChanged(CredentialKind value)
    {
        OnPropertyChanged(nameof(IsPasswordAuth));
        OnPropertyChanged(nameof(IsPrivateKeyAuth));
        OnPropertyChanged(nameof(AuthModeIndex));
    }

    partial void OnStatusMessageChanged(string? value)
        => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnPresentedFingerprintChanged(string? value)
        => OnPropertyChanged(nameof(HasPresentedFingerprint));

    partial void OnKnownFingerprintChanged(string? value)
        => OnPropertyChanged(nameof(HasKnownFingerprint));

    [RelayCommand]
    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        ClearStatus();
        IsBusy = true;
        try
        {
            var request = BuildTestRequest();
            var result = await _testConnection.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            ApplyTestResult(result);
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (OperationCanceledException)
        {
            SetError("Test annulé.");
        }
        finally
        {
            IsBusy = false;
            SaveCommand.NotifyCanExecuteChanged();
            ApproveHostKeyCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApproveHostKeyNow))]
    private async Task ApproveHostKeyAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(PresentedFingerprint))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _approveHostKey.ExecuteAsync(
                new ApproveHostKeyDto
                {
                    Host = Address.Trim(),
                    Port = Port,
                    Fingerprint = PresentedFingerprint,
                },
                cancellationToken).ConfigureAwait(true);

            StatusMessage = "Clé d'hôte approuvée. Relancez le test de connexion.";
            IsError = false;
            IsSecurityWarning = false;
            CanApproveHostKey = false;
            PresentedFingerprint = null;
            KnownFingerprint = null;
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        finally
        {
            IsBusy = false;
            ApproveHostKeyCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanApproveHostKeyNow() => CanApproveHostKey && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ClearStatus();
        IsBusy = true;
        try
        {
            var dto = BuildCreateDto();
            await _addServer.ExecuteAsync(dto, cancellationToken).ConfigureAwait(true);
            if (CloseHandler is not null)
            {
                await CloseHandler(true).ConfigureAwait(true);
            }
        }
        catch (ValidationException exception)
        {
            SetError(exception.Message);
        }
        catch (HostDeck.Application.Errors.HostDeckException exception)
        {
            SetError(exception.UserMessage);
        }
        finally
        {
            IsBusy = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSave() => ConnectionSucceeded && !IsBusy;

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (CloseHandler is not null)
        {
            await CloseHandler(false).ConfigureAwait(true);
        }
    }

    private TestConnectionRequestDto BuildTestRequest() => new()
    {
        Address = Address.Trim(),
        Port = Port,
        Username = Username.Trim(),
        CredentialKind = CredentialKind,
        Secret = Secret,
        Passphrase = string.IsNullOrWhiteSpace(Passphrase) ? null : Passphrase,
        JumpHost = BuildJumpHost(),
    };

    private CreateServerDto BuildCreateDto() => new()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? Address.Trim() : Name.Trim(),
        Address = Address.Trim(),
        Port = Port,
        Username = Username.Trim(),
        CredentialKind = CredentialKind,
        Secret = Secret,
        Passphrase = string.IsNullOrWhiteSpace(Passphrase) ? null : Passphrase,
        MonitoringIntervalSeconds = MonitoringIntervalSeconds,
        DockerEnabled = DockerEnabled,
        Group = string.IsNullOrWhiteSpace(Group) ? null : Group.Trim(),
        JumpHost = BuildJumpHost(),
    };

    private JumpHostDto? BuildJumpHost()
    {
        if (!UseJumpHost)
        {
            return null;
        }

        return new JumpHostDto
        {
            Address = JumpAddress.Trim(),
            Port = JumpPort,
            Username = JumpUsername.Trim(),
            CredentialKey = JumpCredentialKey.Trim(),
            CredentialKind = CredentialKind.PrivateKey,
        };
    }

    private void ApplyTestResult(TestConnectionResultDto result)
    {
        ConnectionSucceeded = result.IsSuccess;
        PresentedFingerprint = result.PresentedFingerprint;
        KnownFingerprint = result.KnownFingerprint;

        switch (result.Outcome)
        {
            case TestConnectionOutcome.Success:
                StatusMessage = string.IsNullOrWhiteSpace(result.DetectedOperatingSystem)
                    ? result.Message
                    : $"{result.Message} Système détecté : {result.DetectedOperatingSystem}.";
                IsError = false;
                IsSecurityWarning = false;
                CanApproveHostKey = false;
                break;

            case TestConnectionOutcome.HostKeyUnknown:
                StatusMessage = result.Message;
                IsError = true;
                IsSecurityWarning = false;
                CanApproveHostKey = !string.IsNullOrWhiteSpace(result.PresentedFingerprint);
                // TODO: brancher un dialogue dédié d'approbation hors formulaire si le flux Application
                // expose un pending-approval plus riche (empreinte + méta SSH).
                break;

            case TestConnectionOutcome.HostKeyChanged:
                StatusMessage = result.Message;
                IsError = true;
                IsSecurityWarning = true;
                CanApproveHostKey = !string.IsNullOrWhiteSpace(result.PresentedFingerprint);
                break;

            default:
                StatusMessage = result.Message;
                IsError = true;
                IsSecurityWarning = false;
                CanApproveHostKey = false;
                break;
        }
    }

    private void ClearStatus()
    {
        StatusMessage = null;
        IsError = false;
        IsSecurityWarning = false;
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        IsError = true;
        IsSecurityWarning = false;
        ConnectionSucceeded = false;
    }
}
