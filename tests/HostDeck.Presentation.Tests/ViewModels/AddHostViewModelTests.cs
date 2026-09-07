using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Ports;
using HostDeck.Application.Servers;
using HostDeck.Application.Servers.Validation;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.ViewModels.Servers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Presentation.Tests.ViewModels;

public sealed class AddHostViewModelTests
{
    [Fact]
    public async Task SaveRequiresSuccessfulConnectionFirst()
    {
        var vm = CreateViewModel();
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.Address = "10.0.1.10";
        vm.Username = "hostdeck";
        vm.Secret = "key-material";

        await vm.TestConnectionCommand.ExecuteAsync(null);
        Assert.True(vm.ConnectionSucceeded);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task HostKeyUnknownExposesApproveAction()
    {
        var ssh = new HostKeySshFactory(changed: false);
        var vm = CreateViewModel(ssh);
        vm.Address = "10.0.1.10";
        vm.Username = "hostdeck";
        vm.Secret = "key-material";

        await vm.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(vm.CanApproveHostKey);
        Assert.Equal("SHA256:presented", vm.PresentedFingerprint);
        Assert.False(vm.ConnectionSucceeded);
    }

    private static AddHostViewModel CreateViewModel(ISshConnectionFactory? ssh = null)
    {
        var credentials = new MemoryCredentialStore();
        var servers = new MemoryServerRepository();
        var hostKeys = new MemoryHostKeyStore();
        ssh ??= new SuccessSshFactory();

        return new AddHostViewModel(
            new AddServerUseCase(
                servers,
                credentials,
                new CreateServerDtoValidator(),
                NullLogger<AddServerUseCase>.Instance),
            new TestConnectionUseCase(
                ssh,
                credentials,
                new TestConnectionRequestDtoValidator(),
                NullLogger<TestConnectionUseCase>.Instance),
            new ApproveHostKeyUseCase(
                hostKeys,
                new ApproveHostKeyDtoValidator(),
                NullLogger<ApproveHostKeyUseCase>.Instance));
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _secrets = [];

        public Task<SecretMaterial> ReadAsync(CredentialReference reference, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SecretMaterial(_secrets[reference.Key]));
        }

        public Task WriteAsync(CredentialReference reference, ReadOnlyMemory<char> secret, CancellationToken cancellationToken = default)
        {
            _secrets[reference.Key] = secret.ToString();
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CredentialReference reference, CancellationToken cancellationToken = default)
        {
            _secrets.Remove(reference.Key);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(CredentialReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(_secrets.ContainsKey(reference.Key));

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class MemoryServerRepository : IServerRepository
    {
        public Task<System.Collections.Generic.IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<System.Collections.Generic.IReadOnlyList<Server>>([]);

        public Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default)
            => Task.FromResult<Server?>(null);

        public Task AddAsync(Server server, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Server server, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsWithEndpointAsync(HostAddress address, Port port, ServerId? excluding = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class MemoryHostKeyStore : IHostKeyStore
    {
        public Task<string?> FindApprovedFingerprintAsync(string host, int port, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task ApproveAsync(string host, int port, string fingerprint, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RevokeAsync(string host, int port, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class SuccessSshFactory : ISshConnectionFactory
    {
        public Task<ISshConnection> ConnectAsync(Server server, CancellationToken cancellationToken = default)
            => Task.FromResult<ISshConnection>(new OkConnection());
    }

    private sealed class HostKeySshFactory(bool changed) : ISshConnectionFactory
    {
        public Task<ISshConnection> ConnectAsync(Server server, CancellationToken cancellationToken = default)
            => throw new HostDeck.Application.Errors.HostKeyVerificationException(
                $"{server.Address.Value}:{server.Port.Value}",
                "SHA256:presented",
                changed ? "SHA256:known" : null);
    }

    private sealed class OkConnection : ISshConnection
    {
        public string Host => "10.0.1.10";
        public TimeSpan Latency => TimeSpan.FromMilliseconds(5);
        public Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommandResult
            {
                StandardOutput = "Linux",
                StandardError = string.Empty,
                ExitCode = 0,
            });
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
