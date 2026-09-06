using Avalonia.Controls;

namespace HostDeck.Presentation.Views.Shell;

/// <summary>
/// Fenêtre principale de HostDeck. Toute la logique reste dans les ViewModels : ce code-behind
/// ne doit contenir que de l'interaction purement visuelle (§22).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
