using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Résultat du parse de <c>/proc/meminfo</c> : mémoire vive et swap en valeurs du domaine.
/// </summary>
internal sealed record MemInfoSnapshot
{
    public required MemoryUsage Memory { get; init; }

    public required SwapUsage Swap { get; init; }
}
