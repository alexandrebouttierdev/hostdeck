using System.Threading.Tasks;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

public sealed class HostKeyStoreTests : RepositoryTestBase
{
    [Fact]
    public async Task ApproveThenFind_ReturnsFingerprint()
    {
        await HostKeys.ApproveAsync("10.0.1.10", 22, "SHA256:aaaa", Ct);

        var fingerprint = await HostKeys.FindApprovedFingerprintAsync("10.0.1.10", 22, Ct);

        Assert.Equal("SHA256:aaaa", fingerprint);
    }

    [Fact]
    public async Task Find_ReturnsNullForUnknownHost()
    {
        Assert.Null(await HostKeys.FindApprovedFingerprintAsync("10.0.1.99", 22, Ct));
    }

    [Fact]
    public async Task Find_IsScopedByPort()
    {
        await HostKeys.ApproveAsync("10.0.1.10", 22, "SHA256:port22", Ct);

        Assert.Null(await HostKeys.FindApprovedFingerprintAsync("10.0.1.10", 2222, Ct));
        Assert.Equal("SHA256:port22", await HostKeys.FindApprovedFingerprintAsync("10.0.1.10", 22, Ct));
    }

    [Fact]
    public async Task ApproveTwice_ReplacesFingerprintWithoutDuplicating()
    {
        await HostKeys.ApproveAsync("10.0.1.10", 22, "SHA256:ancienne", Ct);
        await HostKeys.ApproveAsync("10.0.1.10", 22, "SHA256:nouvelle", Ct);

        Assert.Equal("SHA256:nouvelle", await HostKeys.FindApprovedFingerprintAsync("10.0.1.10", 22, Ct));
    }

    [Fact]
    public async Task Revoke_RemovesApproval()
    {
        await HostKeys.ApproveAsync("10.0.1.10", 22, "SHA256:aaaa", Ct);

        await HostKeys.RevokeAsync("10.0.1.10", 22, Ct);

        Assert.Null(await HostKeys.FindApprovedFingerprintAsync("10.0.1.10", 22, Ct));
    }
}
