namespace HostDeck.Domain.Incidents;

/// <summary>
/// Cycle de vie d'un incident.
///
/// « Rétabli » et « résolu » sont distincts, et c'est essentiel : la condition peut cesser
/// sans que personne n'ait constaté l'incident. Les fusionner ferait disparaître de la vue
/// des pannes réelles que personne n'a jamais vues.
/// </summary>
public enum IncidentStatus
{
    /// <summary>La condition est active et personne ne l'a prise en charge.</summary>
    Open = 0,

    /// <summary>Un opérateur a acquitté l'incident. La condition peut rester active.</summary>
    Acknowledged = 1,

    /// <summary>La condition a cessé d'elle-même, sans clôture explicite.</summary>
    Recovered = 2,

    /// <summary>L'incident est clos. État terminal.</summary>
    Resolved = 3,
}
