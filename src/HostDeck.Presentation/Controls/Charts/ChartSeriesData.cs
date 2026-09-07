using System.Collections.Generic;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls.Charts;

/// <summary>
/// Série légère pour les graphiques Presentation. Pas de VM par point : une liste de valeurs
/// déjà downsamplées côté Application / dépôt (§19, UI_DESIGN).
/// </summary>
public sealed class ChartSeriesData
{
    public required string Name { get; init; }

    public required IReadOnlyList<float> Values { get; init; }

    public Color Color { get; init; }
}
