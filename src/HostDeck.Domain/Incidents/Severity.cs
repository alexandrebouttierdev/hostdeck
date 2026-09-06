namespace HostDeck.Domain.Incidents;

/// <summary>
/// Gravité d'un incident, des cinq niveaux imposés par la spécification (§20).
///
/// Les valeurs sont ordonnées : la comparaison numérique traduit directement une comparaison
/// de gravité, ce dont dépendent le tri des incidents et le choix du niveau le plus élevé.
/// </summary>
public enum Severity
{
    /// <summary>Fait notable, sans action attendue.</summary>
    Information = 0,

    /// <summary>Situation à surveiller.</summary>
    Warning = 1,

    /// <summary>Dégradation confirmée.</summary>
    Average = 2,

    /// <summary>Impact important sur le service.</summary>
    High = 3,

    /// <summary>Perte de service ou risque imminent.</summary>
    Critical = 4,
}
