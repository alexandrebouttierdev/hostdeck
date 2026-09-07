using System;
using System.Globalization;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Quantité d'octets.
///
/// Stockée en <see cref="long"/> : les compteurs réseau cumulés d'un hôte de longue durée
/// dépassent largement <see cref="int"/>, et un débordement silencieux produirait des débits
/// négatifs dans les graphiques.
/// </summary>
public readonly record struct ByteSize : IComparable<ByteSize>
{
    private const long Kibibyte = 1024L;

    public static readonly ByteSize Zero = new(0L);

    public ByteSize(long bytes)
    {
        Bytes = Guard.NotNegative(bytes);
    }

    public long Bytes { get; }

    public double Kibibytes => Bytes / (double)Kibibyte;

    public double Mebibytes => Bytes / (double)(Kibibyte * Kibibyte);

    public double Gibibytes => Bytes / (double)(Kibibyte * Kibibyte * Kibibyte);

    public static ByteSize FromKibibytes(long kibibytes) =>
        new(checked(Guard.NotNegative(kibibytes) * Kibibyte));

    public static ByteSize FromMebibytes(long mebibytes) =>
        new(checked(Guard.NotNegative(mebibytes) * Kibibyte * Kibibyte));

    public int CompareTo(ByteSize other) => Bytes.CompareTo(other.Bytes);

    public static ByteSize operator +(ByteSize left, ByteSize right) =>
        new(checked(left.Bytes + right.Bytes));

    /// <summary>
    /// Différence bornée à zéro. Un compteur réseau qui repart de zéro après un redémarrage
    /// donnerait sinon un delta négatif, donc un débit absurde.
    /// </summary>
    public static ByteSize operator -(ByteSize left, ByteSize right) =>
        new(Math.Max(0L, left.Bytes - right.Bytes));

    public static ByteSize Add(ByteSize left, ByteSize right) => left + right;

    public static ByteSize Subtract(ByteSize left, ByteSize right) => left - right;

    public static bool operator <(ByteSize left, ByteSize right) => left.Bytes < right.Bytes;

    public static bool operator >(ByteSize left, ByteSize right) => left.Bytes > right.Bytes;

    public static bool operator <=(ByteSize left, ByteSize right) => left.Bytes <= right.Bytes;

    public static bool operator >=(ByteSize left, ByteSize right) => left.Bytes >= right.Bytes;

    /// <summary>
    /// Rendu binaire compact, à la manière des outils système (« 58,3 GiB »).
    /// La mise en forme finale destinée à l'écran appartient à la Presentation ; ce rendu
    /// sert au diagnostic et aux tests.
    /// </summary>
    public override string ToString()
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];

        double value = Bytes;
        var unitIndex = 0;
        while (value >= Kibibyte && unitIndex < units.Length - 1)
        {
            value /= Kibibyte;
            unitIndex++;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{value:0.##} {units[unitIndex]}");
    }
}
