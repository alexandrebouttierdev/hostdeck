using System;

namespace HostDeck.Domain.Shared;

/// <summary>
/// Levée quand une tentative de construction violerait un invariant du domaine.
///
/// Elle signale un défaut d'appel, pas une erreur attendue du flux métier : les cas
/// prévisibles (hôte injoignable, authentification refusée) sont modélisés par des états,
/// pas par des exceptions (§38).
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string parameterName, string reason)
        : base($"{parameterName} : {reason}")
    {
        ParameterName = parameterName;
        Reason = reason;
    }

    public DomainValidationException(string parameterName, string reason, Exception innerException)
        : base($"{parameterName} : {reason}", innerException)
    {
        ParameterName = parameterName;
        Reason = reason;
    }

    /// <summary>
    /// Nom du paramètre ou de la propriété fautive, en anglais comme le reste du code.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Raison lisible du rejet, destinée au diagnostic et aux tests.
    /// </summary>
    public string Reason { get; }
}
