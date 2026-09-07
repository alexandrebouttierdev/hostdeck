using System;

namespace HostDeck.Application.Errors;

/// <summary>
/// Un accès à la base locale a échoué.
/// </summary>
public sealed class RepositoryException : HostDeckException
{
    public RepositoryException(string operation, Exception innerException)
        : base($"Repository operation '{operation}' failed", innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }

    public override string UserMessage =>
        "L'accès aux données locales a échoué. Consultez les journaux pour le détail.";
}

/// <summary>
/// Une entité attendue est absente.
///
/// Un type dédié plutôt qu'un <c>null</c> remonté à travers les couches : l'appelant doit
/// choisir explicitement entre traiter l'absence et la propager.
/// </summary>
public sealed class EntityNotFoundException : HostDeckException
{
    public EntityNotFoundException(string entityName, string identifier)
        : base($"{entityName} '{identifier}' was not found")
    {
        EntityName = entityName;
        Identifier = identifier;
    }

    public string EntityName { get; }

    public string Identifier { get; }

    public override string UserMessage => "L'élément demandé est introuvable.";
}

/// <summary>
/// Le trousseau du système est indisponible, ou l'accès a été refusé.
///
/// Il n'existe volontairement aucun repli en clair : si le trousseau ne répond pas,
/// l'opération échoue et le dit (§51, T1).
/// </summary>
public sealed class CredentialException : HostDeckException
{
    public CredentialException(string message, CredentialFailure failure)
        : base(message)
    {
        Failure = failure;
    }

    public CredentialException(string message, CredentialFailure failure, Exception innerException)
        : base(message, innerException)
    {
        Failure = failure;
    }

    public CredentialFailure Failure { get; }

    public override string UserMessage => Failure switch
    {
        CredentialFailure.NotFound =>
            "L'identifiant associé à ce serveur est introuvable dans le trousseau du système.",
        CredentialFailure.StoreUnavailable =>
            "Le trousseau du système est indisponible. HostDeck ne conserve aucun secret en dehors de lui.",
        CredentialFailure.AccessDenied =>
            "L'accès au trousseau du système a été refusé.",
        _ => "Une erreur est survenue lors de l'accès au trousseau du système.",
    };
}

public enum CredentialFailure
{
    NotFound = 0,
    StoreUnavailable = 1,
    AccessDenied = 2,
    WriteFailed = 3,
}

/// <summary>
/// Un appel au moteur Docker a échoué.
/// </summary>
public sealed class DockerException : HostDeckException
{
    public DockerException(string operation, string message)
        : base($"Docker operation '{operation}' failed: {message}")
    {
        Operation = operation;
    }

    public DockerException(string operation, string message, Exception innerException)
        : base($"Docker operation '{operation}' failed: {message}", innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }

    public override string UserMessage => "L'opération Docker a échoué.";
}

/// <summary>
/// Un cycle de collecte n'a pas pu aboutir pour une raison qui ne relève ni de SSH,
/// ni de Docker, ni de la persistance — typiquement une sortie illisible.
/// </summary>
public class MonitoringException : HostDeckException
{
    public MonitoringException(string message)
        : base(message)
    {
    }

    public MonitoringException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string UserMessage => "La collecte des métriques a échoué.";
}

/// <summary>
/// L'envoi d'une notification bureau a échoué. Volontairement non bloquant pour la
/// supervision : perdre une notification ne doit pas interrompre la collecte.
/// </summary>
public sealed class NotificationException : HostDeckException
{
    public NotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string UserMessage => "L'envoi de la notification a échoué.";
}

/// <summary>
/// Une sortie collectée n'a pas le format attendu.
///
/// Portée séparément parce que les hôtes supervisés ne sont pas de confiance : leur sortie
/// est une entrée hostile potentielle, et un parser doit signaler un format inattendu plutôt
/// que lever au hasard depuis les profondeurs d'une conversion (§51, T6).
/// </summary>
public sealed class MetricParseException : MonitoringException
{
    public MetricParseException(string source, string reason)
        : base($"Failed to parse '{source}': {reason}")
    {
        SourceName = source;
        Reason = reason;
    }

    /// <summary>
    /// Origine de la donnée, par exemple « /proc/stat ». Nommé ainsi et non « Source »
    /// pour ne pas masquer <see cref="Exception.Source"/>, défini par la BCL.
    /// </summary>
    public string SourceName { get; }

    public string Reason { get; }

    public override string UserMessage =>
        $"La sortie de {SourceName} n'a pas pu être interprétée.";
}
