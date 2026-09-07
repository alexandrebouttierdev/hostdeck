using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HostDeck.Presentation.Controls;

/// <summary>
/// État vide explicite : titre, description et icône optionnelle.
/// Les panneaux hors périmètre V1 et les listes sans données passent par ce contrôle.
/// </summary>
public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IsCompact));

    public EmptyState()
    {
        InitializeComponent();
        IconDataProperty.Changed.AddClassHandler<EmptyState>((control, _) => control.UpdateIconVisibility());
        IsCompactProperty.Changed.AddClassHandler<EmptyState>((control, _) => control.ApplyCompactLayout());
        AttachedToVisualTree += (_, _) =>
        {
            UpdateIconVisibility();
            ApplyCompactLayout();
        };
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    /// <summary>Réduit icône et titres pour les cartes jauges / panneaux denses.</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private void UpdateIconVisibility()
    {
        if (IconPath is not null)
        {
            IconPath.IsVisible = IconData is not null;
        }
    }

    private void ApplyCompactLayout()
    {
        if (IconPath is not null)
        {
            IconPath.Width = IsCompact ? 22d : 34d;
            IconPath.Height = IsCompact ? 22d : 34d;
        }

        if (TitleBlock is not null)
        {
            TitleBlock.FontSize = IsCompact ? 12d : 16d;
        }

        if (DescriptionBlock is not null)
        {
            DescriptionBlock.MaxWidth = IsCompact ? 220d : 420d;
            DescriptionBlock.FontSize = IsCompact ? 11d : 12d;
        }
    }
}
