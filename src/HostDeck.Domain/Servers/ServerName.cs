using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Nom d'affichage d'un serveur, tel que l'opérateur le saisit.
/// Distinct de l'adresse : deux serveurs peuvent porter le même nom d'hôte réseau.
///
/// Modélisé en type référence et non en <c>struct</c> : <c>default(T)</c> contournerait le
/// constructeur et produirait une instance au contenu nul, donc invalide. L'analyse de
/// nullabilité protège la version référence.
/// </summary>
public sealed record ServerName
{
    public const int MaxLength = 128;

    public ServerName(string value)
    {
        Value = Guard.RequiredText(value, MaxLength);
    }

    public string Value { get; }

    public override string ToString() => Value;
}
