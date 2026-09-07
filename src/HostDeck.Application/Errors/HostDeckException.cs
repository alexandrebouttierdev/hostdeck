using System;

namespace HostDeck.Application.Errors;

/// <summary>
/// Racine des erreurs applicatives de HostDeck.
///
/// Ces types existent pour que la Presentation puisse réagir à une catégorie d'échec sans
/// connaître SSH.NET, EF Core ni le SDK Docker : l'Infrastructure traduit ses exceptions
/// techniques vers cette hiérarchie et conserve l'originale en <see cref="Exception.InnerException"/>
/// (§38).
///
/// Elles ne servent jamais de flux métier normal. Une issue attendue — hôte injoignable,
/// authentification refusée pendant un test de connexion — est modélisée par un état ou un
/// résultat, pas par une exception.
/// </summary>
public abstract class HostDeckException : Exception
{
    protected HostDeckException(string message)
        : base(message)
    {
    }

    protected HostDeckException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Message destiné à l'utilisateur, en français, sans détail technique ni trace
    /// d'appel. La Presentation affiche celui-ci, jamais <see cref="Exception.Message"/>.
    /// </summary>
    public abstract string UserMessage { get; }
}
