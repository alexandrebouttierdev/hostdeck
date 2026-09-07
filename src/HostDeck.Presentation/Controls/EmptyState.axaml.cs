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

    public EmptyState()
    {
        InitializeComponent();
        IconDataProperty.Changed.AddClassHandler<EmptyState>((control, _) => control.UpdateIconVisibility());
        AttachedToVisualTree += (_, _) => UpdateIconVisibility();
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

    private void UpdateIconVisibility()
    {
        if (IconPath is not null)
        {
            IconPath.IsVisible = IconData is not null;
        }
    }
}
