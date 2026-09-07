using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Ports;
using HostDeck.Application.Servers;
using HostDeck.Application.Servers.Validation;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Application.Tests.Servers;

public sealed class TestConnectionUseCaseInlineSecretTests
{
    [Fact]
    public async Task WritesTemporarySecretThenRemovesIt()
    {
        var credentials = new FakeCredentialStore();
        var ssh = new RecordingSshFactory();
        var useCase = new TestConnectionUseCase(
            ssh,
            credentials,
            new TestConnectionRequestDtoValidator(),
            NullLogger<TestConnectionUseCase>.Instance);

        var result = await useCase.ExecuteAsync(
            new TestConnectionRequestDto
            {
                Address = "10.0.1.10",
                Username = "hostdeck",
                Secret = "super-secret-key-material",
                CredentialKind = CredentialKind.PrivateKey,
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(credentials.Secrets);
        Assert.Equal(1, ssh.ConnectCalls);
    }

    private sealed class RecordingSshFactory : ISshConnectionFactory
    {
        public int ConnectCalls { get; private set; }

        public Task<ISshConnection> ConnectAsync(Server server, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ISshConnection>(new FakeConnection());
        }
    }

    private sealed class FakeConnection : ISshConnection
    {
        public string Host => "10.0.1.10";

        public TimeSpan Latency => TimeSpan.FromMilliseconds(12);

        public Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CommandResult
            {
                StandardOutput = "Linux",
                StandardError = string.Empty,
                ExitCode = 0,
            });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
