using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Port TCP. Le type fort empêche d'inverser un port et un intervalle de collecte,
/// deux entiers que rien ne distinguerait autrement.
/// </summary>
public readonly record struct Port
{
    public const int Minimum = 1;
    public const int Maximum = 65535;

    /// <summary>Port SSH par défaut.</summary>
    public static readonly Port DefaultSsh = new(22);

    public Port(int value)
    {
        Value = Guard.InRange(value, Minimum, Maximum);
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
