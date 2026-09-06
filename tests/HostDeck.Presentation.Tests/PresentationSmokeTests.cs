using Avalonia.Headless.XUnit;
using HostDeck.Presentation.Views.Shell;
using Xunit;

namespace HostDeck.Presentation.Tests;

/// <summary>
/// Garde-fou de phase 0 : la fenêtre principale se construit sous le pilote headless Avalonia,
/// ce qui valide la compilation XAML et le harnais de test UI (§46).
/// </summary>
public sealed class PresentationSmokeTests
{
    [AvaloniaFact]
    public void MainWindowCanBeConstructed()
    {
        var window = new MainWindow();

        Assert.Equal("HostDeck", window.Title);
    }
}
