using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Servers;
using HostDeck.Application.Servers.Validation;
using HostDeck.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Application.Tests.Servers;

public sealed class ApproveHostKeyUseCaseTests
{
    [Fact]
    public async Task ApprovesFingerprintInTheStore()
    {
        var store = new FakeHostKeyStore();
        var useCase = new ApproveHostKeyUseCase(
            store,
            new ApproveHostKeyDtoValidator(),
            NullLogger<ApproveHostKeyUseCase>.Instance);

        await useCase.ExecuteAsync(
            new ApproveHostKeyDto
            {
                Host = "10.0.1.10",
                Port = 22,
                Fingerprint = "SHA256:abc",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            "SHA256:abc",
            await store.FindApprovedFingerprintAsync(
                "10.0.1.10",
                22,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsEmptyFingerprint()
    {
        var useCase = new ApproveHostKeyUseCase(
            new FakeHostKeyStore(),
            new ApproveHostKeyDtoValidator(),
            NullLogger<ApproveHostKeyUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(
                new ApproveHostKeyDto
                {
                    Host = "10.0.1.10",
                    Fingerprint = string.Empty,
                },
                TestContext.Current.CancellationToken));
    }
}
