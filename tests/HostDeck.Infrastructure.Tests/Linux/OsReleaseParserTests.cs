using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class OsReleaseParserTests
{
    [Fact]
    public void Parse_ReadsIdentityFields()
    {
        var raw =
            """
            NAME="Ubuntu"
            VERSION="22.04.4 LTS (Jammy Jellyfish)"
            ID=ubuntu
            ID_LIKE=debian
            PRETTY_NAME="Ubuntu 22.04.4 LTS"
            VERSION_CODENAME=jammy
            UBUNTU_CODENAME=jammy
            """;

        var info = OsReleaseParser.Parse(raw);

        Assert.Equal("ubuntu", info.Id);
        Assert.Equal("Ubuntu", info.Name);
        Assert.Equal("22.04.4 LTS (Jammy Jellyfish)", info.Version);
        Assert.Equal("Ubuntu 22.04.4 LTS", info.PrettyName);
        Assert.Equal("Ubuntu 22.04.4 LTS", info.DisplayName);
    }

    [Fact]
    public void Parse_WithoutPrettyName_ComposesDisplayName()
    {
        var raw =
            """
            NAME="Debian"
            VERSION_ID="12"
            ID=debian
            """;

        var info = OsReleaseParser.Parse(raw);

        Assert.Equal("debian", info.Id);
        Assert.Null(info.PrettyName);
        Assert.Equal("Debian 12", info.DisplayName);
    }

    [Fact]
    public void Parse_IgnoresCommentsAndUnknownKeys()
    {
        var raw =
            """
            # commentaire
            NAME='Alpine'
            ID=alpine
            UN_CHAMP_INCONNU="valeur"
            """;

        var info = OsReleaseParser.Parse(raw);

        Assert.Equal("alpine", info.Id);
        Assert.Equal("Alpine", info.Name);
        Assert.Equal("Alpine", info.DisplayName);
    }
}
