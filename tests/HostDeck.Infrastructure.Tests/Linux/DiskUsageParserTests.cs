using System.Linq;
using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class DiskUsageParserTests
{
    private const long KiB = 1024;

    [Fact]
    public void Parse_ReadsBlocksInKibibytesPerMountPoint()
    {
        var raw =
            """
            Filesystem     1024-blocks      Used Available Capacity Mounted on
            /dev/sda1        41271072  12175232  26975576      32% /
            tmpfs            16349712         0  16349712       0% /dev/shm
            /dev/sdb1        99999999  88888888   10000000      95% /var/lib/docker
            """;

        var disks = DiskUsageParser.Parse(raw);

        Assert.Equal(3, disks.Count);

        var root = Assert.Single(disks, disk => disk.MountPoint == "/");
        Assert.Equal("/dev/sda1", root.FileSystem);
        Assert.Equal(41_271_072 * KiB, root.Total.Bytes);
        Assert.Equal(12_175_232 * KiB, root.Used.Bytes);

        var sharedMemory = Assert.Single(disks, disk => disk.MountPoint == "/dev/shm");
        Assert.Equal(0, sharedMemory.Used.Bytes);
    }

    [Fact]
    public void Parse_SkipsUnreadableLinesAndHeader()
    {
        var raw =
            """
            Filesystem     1024-blocks      Used Available Capacity Mounted on
            /dev/sda1        41271072  12175232  26975576      32% /
            /dev/corrompu     abcde   100  200 1% /broken
            """;

        var disks = DiskUsageParser.Parse(raw);

        var disk = Assert.Single(disks);
        Assert.Equal("/", disk.MountPoint);
    }
}
