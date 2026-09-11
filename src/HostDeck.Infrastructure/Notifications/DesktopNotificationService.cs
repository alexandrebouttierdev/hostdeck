using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CA1031
using HostDeck.Application.Ports;
using HostDeck.Domain.Incidents;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Notifications;

/// <summary>
/// Notifications bureau best-effort : <c>notify-send</c> sur Linux, journalisation ailleurs.
/// </summary>
internal sealed class DesktopNotificationService : IDesktopNotificationService
{
    private readonly ILogger<DesktopNotificationService> _logger;

    public DesktopNotificationService(ILogger<DesktopNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task NotifyIncidentAsync(
        IncidentNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            if (OperatingSystem.IsLinux())
            {
                await TryNotifySendAsync(notification, cancellationToken).ConfigureAwait(false);
                return;
            }

            InfrastructureLog.DesktopNotificationLogged(
                _logger,
                notification.Title,
                notification.Body);
        }
        catch (Exception exception)
        {
            InfrastructureLog.DesktopNotificationFailed(_logger, exception);
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(FindNotifySendExecutable() is not null);
    }

    private async Task TryNotifySendAsync(
        IncidentNotification notification,
        CancellationToken cancellationToken)
    {
        var executable = FindNotifySendExecutable();
        if (executable is null)
        {
            InfrastructureLog.DesktopNotificationUnavailable(_logger);
            return;
        }

        var urgency = notification.Severity switch
        {
            Severity.Critical => "critical",
            Severity.High => "normal",
            _ => "low",
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.StartInfo.ArgumentList.Add("--urgency");
        process.StartInfo.ArgumentList.Add(urgency);
        process.StartInfo.ArgumentList.Add("--app-name");
        process.StartInfo.ArgumentList.Add("HostDeck");
        process.StartInfo.ArgumentList.Add(notification.Title);
        process.StartInfo.ArgumentList.Add(notification.Body);

        if (!process.Start())
        {
            InfrastructureLog.DesktopNotificationUnavailable(_logger);
            return;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            InfrastructureLog.DesktopNotificationFailed(
                _logger,
                new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"notify-send exited with code {process.ExitCode}"
                        : error.Trim()));
        }
    }

    private static string? FindNotifySendExecutable()
    {
        if (File.Exists("/usr/bin/notify-send"))
        {
            return "/usr/bin/notify-send";
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "notify-send",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return null;
            }

            var path = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(path) ? path : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
