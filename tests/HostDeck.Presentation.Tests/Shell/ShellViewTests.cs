using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using HostDeck.Presentation.Controls;
using HostDeck.Presentation.Views.Infrastructure;
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
    public void ShellContainsTheCompactChrome()
    {
        var shell = new ShellView();

        var rail = Assert.IsType<Border>(shell.FindControl<Border>("NavigationRail"));
        var topbar = Assert.IsType<Border>(shell.FindControl<Border>("Topbar"));
        var statusBar = Assert.IsType<Border>(shell.FindControl<Border>("StatusBar"));

        Assert.Equal(66d, rail.Width);
        Assert.Equal(49d, topbar.Height);
        Assert.Equal(40d, statusBar.Height);
    }

    [AvaloniaFact]
    public void InfrastructureEmptyStateUsesExplicitFrenchCopy()
    {
        var view = new InfrastructureView();

        // Les états vides vivent dans les écrans, plus dans le chrome du shell.
        var empty = Assert.IsType<EmptyState>(view.FindControl<EmptyState>("FleetEmptyState"));
        Assert.Equal("Aucun hôte configuré", empty.Title);
        Assert.Equal(
            "Ajoutez votre premier serveur Linux pour commencer la supervision.",
            empty.Description);
    }

    [AvaloniaFact]
    public void InfrastructureDetailPanelExistsForSelection()
    {
        var view = new InfrastructureView();
        var panel = Assert.IsType<Border>(view.FindControl<Border>("HostDetailPanel"));
        Assert.NotNull(panel);
    }
}
