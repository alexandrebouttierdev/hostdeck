using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using HostDeck.Presentation.Tests;
// Le namespace HostDeck.Application masque le type Avalonia.Application depuis
// l'intérieur de HostDeck.* : l'alias désigne sans ambiguïté la classe Avalonia.
using AvaloniaApplication = Avalonia.Application;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace HostDeck.Presentation.Tests;

/// <summary>
/// Application Avalonia headless utilisée par toute la suite de tests de présentation.
/// Elle charge le même thème que l'application réelle pour que les tests portent sur
/// les styles effectivement livrés (§46).
/// </summary>
public sealed class TestApplication : AvaloniaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
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
