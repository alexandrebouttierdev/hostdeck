using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using HostDeck.Presentation.ViewModels.Servers;
using HostDeck.Presentation.Views.Servers;
using Microsoft.Extensions.DependencyInjection;

namespace HostDeck.Presentation.Services;

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly IServiceProvider _services;

    public AvaloniaDialogService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> ShowAddHostAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner is null)
        {
            return false;
        }

        var viewModel = _services.GetRequiredService<AddHostViewModel>();
        var window = new AddHostWindow
        {
            DataContext = viewModel,
        };

        viewModel.CloseHandler = result =>
        {
            window.Close(result);
            return Task.CompletedTask;
        };

        var result = await window.ShowDialog<bool?>(owner).ConfigureAwait(true);
        return result == true;
    }
}
