using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using HostDeck.Presentation.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace HostDeck.Presentation.Tests;

/// <summary>
/// Application Avalonia headless utilisée par toute la suite de tests de présentation.
/// Elle charge le même thème que l'application réelle pour que les tests portent sur
/// les styles effectivement livrés (§46).
/// </summary>
public sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

/// <summary>
/// Construit l'application de test. Déclaré une seule fois pour le projet.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
