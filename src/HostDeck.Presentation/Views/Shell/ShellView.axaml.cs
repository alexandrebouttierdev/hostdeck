using Avalonia.Controls;

namespace HostDeck.Presentation.Views.Shell;

/// <summary>
/// Coquille visuelle partagée par les écrans. La navigation et les contenus seront pilotés
/// par le ViewModel de shell ; ce code-behind ne porte aucune logique métier.
/// </summary>
public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
    }
}
