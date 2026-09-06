using System;
using Avalonia;
using Avalonia.Controls;

namespace HostDeck.Presentation.Views.Shell;

/// <summary>
/// Dimensionne la fenêtre principale en fonction de l'écran réel.
///
/// <para>
/// Les tailles Avalonia sont exprimées en pixels indépendants du périphérique. Fixer
/// 1672x941 en dur revient à supposer un écran à 96 ppp : sur un écran HiDPI réglé à
/// 288 ppp — facteur 3 — cela demande 5016x2823 pixels physiques, davantage que la dalle.
/// Le gestionnaire de fenêtres tronque alors la demande, la fenêtre occupe tout l'écran, et
/// une coquille encore vide donne l'impression d'un écran noir.
/// </para>
///
/// <para>
/// La taille de conception des maquettes reste la cible ; elle est simplement bornée à ce que
/// l'écran peut réellement afficher (§58, vérification HiDPI).
/// </para>
/// </summary>
public static class WindowSizing
{
    /// <summary>Taille de conception des maquettes, en pixels indépendants du périphérique.</summary>
    public const double DesignWidth = 1672d;

    /// <summary>Hauteur de conception des maquettes.</summary>
    public const double DesignHeight = 941d;

    /// <summary>
    /// Proportion de la zone de travail que la fenêtre occupe au plus, pour laisser visibles
    /// la barre des tâches et les bords de l'écran.
    /// </summary>
    private const double MaximumScreenUsage = 0.92d;

    /// <summary>
    /// Applique à la fenêtre la plus grande taille tenant à la fois dans la taille de
    /// conception et dans l'écran qui l'accueille.
    /// </summary>
    public static void ApplyPreferredSize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var (width, height) = ComputePreferredSize(window);

        window.Width = width;
        window.Height = height;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private static (double Width, double Height) ComputePreferredSize(Window window)
    {
        // Screens est indisponible tant qu'aucun backend de fenêtrage n'est actif, ce qui est
        // le cas en test headless : on retombe alors sur la taille de conception.
        var screen = window.Screens?.ScreenFromWindow(window) ?? window.Screens?.Primary;

        if (screen is null)
        {
            return (DesignWidth, DesignHeight);
        }

        // WorkingArea est en pixels physiques ; Scaling convertit vers les unités logiques
        // dans lesquelles Width et Height sont exprimés.
        var preferred = CalculatePreferredSize(
            screen.WorkingArea.Size,
            screen.Scaling,
            new Size(window.MinWidth, window.MinHeight));

        return (preferred.Width, preferred.Height);
    }

    /// <summary>
    /// Calcule la taille logique à partir d'une zone de travail exprimée en pixels physiques.
    /// Cette surcharge pure garde le calcul vérifiable sans dépendre d'un serveur d'affichage.
    /// </summary>
    public static Size CalculatePreferredSize(
        PixelSize physicalWorkingArea,
        double scaling,
        Size minimumSize)
    {
        var validScaling = double.IsFinite(scaling) && scaling > 0 ? scaling : 1d;
        var availableWidth = physicalWorkingArea.Width / validScaling * MaximumScreenUsage;
        var availableHeight = physicalWorkingArea.Height / validScaling * MaximumScreenUsage;

        return new Size(
            Clamp(DesignWidth, availableWidth, minimumSize.Width),
            Clamp(DesignHeight, availableHeight, minimumSize.Height));
    }

    /// <summary>
    /// Réduit la dimension souhaitée à ce que l'écran offre, sans jamais descendre sous la
    /// taille minimale de la fenêtre : une fenêtre plus petite que son propre minimum serait
    /// immédiatement réagrandie par le gestionnaire de fenêtres.
    /// </summary>
    private static double Clamp(double desired, double available, double minimum)
    {
        var bounded = Math.Min(desired, available);

        return double.IsFinite(minimum) && minimum > 0
            ? Math.Max(bounded, minimum)
            : bounded;
    }
}
