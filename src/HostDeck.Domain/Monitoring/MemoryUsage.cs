using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Occupation mémoire, décomposée comme <c>/proc/meminfo</c> la rapporte.
///
/// Le cache et les tampons sont conservés à part parce qu'ils sont récupérables : les compter
/// comme « utilisés » ferait paraître saturée une machine Linux parfaitement saine. C'est
/// aussi la décomposition qu'affichent les maquettes (utilisée / cache / tampon / libre).
/// </summary>
public sealed record MemoryUsage
{
    public MemoryUsage(ByteSize total, ByteSize used, ByteSize cached, ByteSize buffers)
    {
        if (used > total)
        {
            throw new DomainValidationException(
                nameof(used),
                "la mémoire utilisée ne peut pas dépasser la mémoire totale");
        }

        Total = total;
        Used = used;
        Cached = cached;
        Buffers = buffers;
    }

    public ByteSize Total { get; }

    /// <summary>Mémoire réellement occupée, cache et tampons exclus.</summary>
    public ByteSize Used { get; }

    public ByteSize Cached { get; }

    public ByteSize Buffers { get; }

    public ByteSize Available => Total - Used;

    public Percentage UsedRatio => Percentage.OfRatio(Used.Bytes, Total.Bytes);
}

/// <summary>
/// Occupation du swap. Séparée de la mémoire vive : une machine sans swap est un cas normal,
/// pas une mesure manquante.
/// </summary>
public sealed record SwapUsage
{
    public static readonly SwapUsage None = new(ByteSize.Zero, ByteSize.Zero);

    public SwapUsage(ByteSize total, ByteSize used)
    {
        if (used > total)
        {
            throw new DomainValidationException(
                nameof(used),
                "le swap utilisé ne peut pas dépasser le swap total");
        }

        Total = total;
        Used = used;
    }

    public ByteSize Total { get; }

    public ByteSize Used { get; }

    public bool IsConfigured => Total.Bytes > 0;

    public Percentage UsedRatio => Percentage.OfRatio(Used.Bytes, Total.Bytes);
}
