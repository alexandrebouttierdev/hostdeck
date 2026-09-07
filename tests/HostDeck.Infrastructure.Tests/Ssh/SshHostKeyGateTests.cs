using HostDeck.Application.Errors;
using HostDeck.Infrastructure.Ssh;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Ssh;

public sealed class SshHostKeyGateTests
{
    [Fact]
    public void Apply_UnknownHostKey_Rejects()
    {
        var gate = new SshHostKeyGate();
        var trusted = true;

        gate.Apply("10.0.0.1", 22, knownFingerprint: null, "ABC", value => trusted = value);

        Assert.False(trusted);
        var rejection = Assert.IsType<HostKeyVerificationException>(Record.Exception(gate.ThrowIfRejected));
        Assert.False(rejection.IsKeyChange);
        Assert.Equal("ABC", rejection.PresentedFingerprint);
    }

    [Fact]
    public void Apply_MatchingFingerprint_Trusts()
    {
        var gate = new SshHostKeyGate();
        var trusted = false;

        gate.Apply("10.0.0.1", 22, knownFingerprint: "KNOWN", "KNOWN", value => trusted = value);

        Assert.True(trusted);
        Assert.Null(Record.Exception(gate.ThrowIfRejected));
    }

    [Fact]
    public void Apply_ChangedFingerprint_IsKeyChange()
    {
        var gate = new SshHostKeyGate();
        var trusted = true;

        gate.Apply("10.0.0.1", 22, knownFingerprint: "OLD", "NEW", value => trusted = value);

        Assert.False(trusted);
        var rejection = Assert.IsType<HostKeyVerificationException>(Record.Exception(gate.ThrowIfRejected));
        Assert.True(rejection.IsKeyChange);
        Assert.Equal("OLD", rejection.KnownFingerprint);
        Assert.Equal("NEW", rejection.PresentedFingerprint);
    }
}
