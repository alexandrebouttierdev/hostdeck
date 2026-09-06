using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using HostDeck.Presentation.Views.Shell;
using Xunit;

namespace HostDeck.Presentation.Tests.Shell;

public sealed class ShellViewTests
{
    [AvaloniaFact]
    public void MainWindowHostsTheApplicationShell()
    {
        var window = new MainWindow();

        Assert.IsType<ShellView>(window.Content);
    }

    [AvaloniaFact]
    public void ShellContainsTheCompactChromeAndExplicitEmptyState()
    {
        var shell = new ShellView();

        var rail = Assert.IsType<Border>(shell.FindControl<Border>("NavigationRail"));
        var topbar = Assert.IsType<Border>(shell.FindControl<Border>("Topbar"));
        var statusBar = Assert.IsType<Border>(shell.FindControl<Border>("StatusBar"));
        var emptyState = Assert.IsType<TextBlock>(shell.FindControl<TextBlock>("EmptyStateMessage"));

        Assert.Equal(66d, rail.Width);
        Assert.Equal(49d, topbar.Height);
        Assert.Equal(40d, statusBar.Height);
        Assert.Equal("Aucun hôte configuré", emptyState.Text);
    }
}
