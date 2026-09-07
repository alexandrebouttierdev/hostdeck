using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Occupation d'un point de montage.
///
/// Un hôte en possède plusieurs : ils sont modélisés un par un et jamais agrégés en un
/// pourcentage unique, car c'est la partition la plus remplie qui déclenche l'incident,
/// pas la moyenne.
/// </summary>
public sealed record DiskUsage
{
    public const int MaxMountPointLength = 4096;

    public DiskUsage(string mountPoint, string? fileSystem, ByteSize total, ByteSize used)
    {
        if (used > total)
        {
            throw new DomainValidationException(
                nameof(used),
                "l'espace utilisé ne peut pas dépasser la capacité totale");
        }

        MountPoint = Guard.RequiredText(mountPoint, MaxMountPointLength);
        FileSystem = string.IsNullOrWhiteSpace(fileSystem) ? null : fileSystem.Trim();
        Total = total;
        Used = used;
    }

    public string MountPoint { get; }

    /// <summary>Périphérique ou source du montage, quand la source la rapporte.</summary>
    public string? FileSystem { get; }

    public ByteSize Total { get; }

    public ByteSize Used { get; }

    public ByteSize Available => Total - Used;

    public Percentage UsedRatio => Percentage.OfRatio(Used.Bytes, Total.Bytes);
}
