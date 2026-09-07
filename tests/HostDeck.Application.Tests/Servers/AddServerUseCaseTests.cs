using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Servers;
using HostDeck.Application.Servers.Validation;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Application.Tests.Servers;

public sealed class AddServerUseCaseTests
{
    private readonly FakeServerRepository _servers = new();
    private readonly FakeCredentialStore _credentials = new();
    private readonly AddServerUseCase _useCase;

    /// <summary>
    /// Jeton d'annulation du test en cours. Le propager rend la suite réactive à une
    /// interruption et exerce au passage le chemin d'annulation des use cases.
    /// </summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AddServerUseCaseTests()
    {
        _useCase = new AddServerUseCase(
            _servers,
            _credentials,
            new CreateServerDtoValidator(),
            NullLogger<AddServerUseCase>.Instance);
    }

    private static CreateServerDto ValidRequest(
        string address = "10.0.1.10",
        string name = "web-front-01",
        JumpHostDto? jumpHost = null) => new()
        {
            Name = name,
            Address = address,
            Port = 22,
            Username = "hostdeck",
            Secret = "-----BEGIN OPENSSH PRIVATE KEY-----",
            MonitoringIntervalSeconds = 60,
            JumpHost = jumpHost,
        };

    [Fact]
    public async Task AddsServerAndStoresSecretInTheKeychain()
    {
        var summary = await _useCase.ExecuteAsync(ValidRequest(), Ct);

        Assert.Equal("web-front-01", summary.Name);
        Assert.Equal(ServerStatus.Unknown, summary.Status);
        Assert.Equal(1, _servers.AddCalls);
        Assert.Single(_credentials.Secrets);
    }

    /// <summary>
    /// Le secret ne doit jamais être renvoyé par une lecture : le DTO de sortie ne le porte
    /// pas, et sa clé de trousseau ne doit rien en révéler (§14, §30).
    /// </summary>
    [Fact]
    public async Task NeverReturnsTheSecret()
    {
        var request = ValidRequest();

        var summary = await _useCase.ExecuteAsync(request, Ct);
        var storedKey = _credentials.Secrets.Keys.Single();

        Assert.DoesNotContain(request.Secret, storedKey, StringComparison.Ordinal);
        Assert.Contains(summary.ServerId.ToString("D"), storedKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Si l'écriture en base échoue après le dépôt du secret, celui-ci doit être retiré :
    /// sinon le trousseau accumule des entrées que plus rien ne référence.
    /// </summary>
    [Fact]
    public async Task RemovesTheStoredSecretWhenThePersistFails()
    {
        _servers.FailNextWriteWith = new InvalidOperationException("database is locked");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _useCase.ExecuteAsync(ValidRequest(), Ct));

        Assert.Empty(_credentials.Secrets);
        Assert.Equal(1, _credentials.DeleteCalls);
    }

    /// <summary>
    /// Si le trousseau refuse le secret, aucun serveur n'est créé : un hôte sans identifiant
    /// utilisable ne serait jamais collectable.
    /// </summary>
    [Fact]
    public async Task DoesNotCreateTheServerWhenTheKeychainRefuses()
    {
        _credentials.FailNextWriteWith = new Errors.CredentialException(
            "keychain locked",
            Errors.CredentialFailure.StoreUnavailable);

        await Assert.ThrowsAsync<Errors.CredentialException>(
            () => _useCase.ExecuteAsync(ValidRequest(), Ct));

        Assert.Equal(0, _servers.AddCalls);
    }

    [Fact]
    public async Task RejectsADuplicateEndpoint()
    {
        await _useCase.ExecuteAsync(ValidRequest("10.0.1.10"), Ct);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _useCase.ExecuteAsync(ValidRequest("10.0.1.10", name: "web-front-02"), Ct));

        Assert.Contains("10.0.1.10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowsTheSameAddressOnADifferentPort()
    {
        await _useCase.ExecuteAsync(ValidRequest("10.0.1.10"), Ct);

        var second = ValidRequest("10.0.1.10", name: "web-front-02") with { Port = 2222 };

        var summary = await _useCase.ExecuteAsync(second, Ct);

        Assert.Equal("web-front-02", summary.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("host;rm -rf /")]
    [InlineData("has space")]
    public async Task RejectsAnInvalidAddress(string address) =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _useCase.ExecuteAsync(ValidRequest(address), Ct));

    [Fact]
    public async Task RejectsAnEmptySecret()
    {
        var request = ValidRequest() with { Secret = string.Empty };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task RejectsAPortOutOfRange(int port)
    {
        var request = ValidRequest() with { Port = port };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7200)]
    public async Task RejectsAMonitoringIntervalOutOfRange(int seconds)
    {
        var request = ValidRequest() with { MonitoringIntervalSeconds = seconds };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    /// <summary>Cas de cycle du §41, refusé avant même d'atteindre le domaine.</summary>
    [Fact]
    public async Task RejectsAJumpHostThatIsTheTargetItself()
    {
        var request = ValidRequest(
            "10.0.1.10",
            jumpHost: new JumpHostDto
            {
                Address = "10.0.1.10",
                Port = 22,
                Username = "bastion",
                CredentialKey = "hostdeck:bastion:1",
            });

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _useCase.ExecuteAsync(request, Ct));

        Assert.Contains("bastion", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptsAValidJumpHost()
    {
        var request = ValidRequest(
            "10.0.1.10",
            jumpHost: new JumpHostDto
            {
                Address = "10.0.0.1",
                Port = 22,
                Username = "bastion",
                CredentialKey = "hostdeck:bastion:1",
            });

        var summary = await _useCase.ExecuteAsync(request, Ct);

        Assert.Equal(ConnectionMode.JumpHost, summary.ConnectionMode);
    }

    [Fact]
    public async Task RejectsAJumpHostWithoutACredential()
    {
        var request = ValidRequest(
            "10.0.1.10",
            jumpHost: new JumpHostDto
            {
                Address = "10.0.0.1",
                Port = 22,
                Username = "bastion",
                CredentialKey = string.Empty,
            });

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task NormalisesTags()
    {
        var request = ValidRequest() with { Tags = ["PROD", "web", "prod"] };

        var summary = await _useCase.ExecuteAsync(request, Ct);

        Assert.Equal(2, summary.Tags.Count);
        Assert.Contains("prod", summary.Tags, StringComparer.Ordinal);
    }

    /// <summary>
    /// L'annulation doit être respectée avant tout effet de bord : ni serveur créé, ni
    /// secret déposé (§61).
    /// </summary>
    [Fact]
    public async Task HonoursCancellation()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _useCase.ExecuteAsync(ValidRequest(), cancelled.Token));

        Assert.Equal(0, _servers.AddCalls);
        Assert.Empty(_credentials.Secrets);
    }

    [Fact]
    public async Task RejectsANullRequest() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => _useCase.ExecuteAsync(null!, Ct));
}
