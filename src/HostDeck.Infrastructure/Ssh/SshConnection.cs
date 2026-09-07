// Adaptateur de frontière : les exceptions tierces (SSH.NET, Latchkey, OS) sont
// converties en erreurs Application typées ; CA1031 est donc désactivé ici.
#pragma warning disable CA1031
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using Renci.SshNet;

namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Session SSH ouverte. Possède le <see cref="SshClient"/> cible et, le cas échéant, le
/// bastion et le port forwardé — rien de tout cela ne quitte l'Infrastructure (§11, §12).
/// </summary>
internal sealed class SshConnection : ISshConnection
{
    private readonly SshClient _client;
    private readonly SshClient? _jumpClient;
    private readonly ForwardedPortLocal? _forwardedPort;
    private readonly SshConnectionOptions _options;
    private readonly TimeSpan _latency;
    private bool _disposed;

    public SshConnection(
        SshClient client,
        string host,
        TimeSpan latency,
        SshConnectionOptions options,
        SshClient? jumpClient = null,
        ForwardedPortLocal? forwardedPort = null)
    {
        _client = client;
        Host = host;
        _latency = latency;
        _options = options;
        _jumpClient = jumpClient;
        _forwardedPort = forwardedPort;
    }

    public string Host { get; }

    public TimeSpan Latency => _latency;

    public async Task<CommandResult> ExecuteAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_client.IsConnected)
        {
            throw new SshConnectionException(Host, "session is not connected");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var commandHandle = _client.CreateCommand(command);
            commandHandle.CommandTimeout = _options.ConnectTimeout;

            // ExecuteAsync de SSH.NET écrit stdout/stderr dans des buffers internes.
            await commandHandle.ExecuteAsync(cancellationToken).ConfigureAwait(false);

            var (stdout, stdoutTruncated) = Truncate(commandHandle.Result, _options.MaxOutputBytes);
            var (stderr, stderrTruncated) = Truncate(commandHandle.Error, _options.MaxOutputBytes);

            return new CommandResult
            {
                StandardOutput = stdout,
                StandardError = stderr,
                ExitCode = commandHandle.ExitStatus ?? -1,
                Duration = stopwatch.Elapsed,
                WasTruncated = stdoutTruncated || stderrTruncated,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SshCommandException(Host, command, exitCode: -1, standardError: exception.GetType().Name);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_forwardedPort is { IsStarted: true })
            {
                _forwardedPort.Stop();
            }
        }
        catch
        {
            // Best-effort à la fermeture.
        }

        _forwardedPort?.Dispose();

        await DisconnectAndDisposeAsync(_client).ConfigureAwait(false);

        if (_jumpClient is not null)
        {
            await DisconnectAndDisposeAsync(_jumpClient).ConfigureAwait(false);
        }
    }

    private static async Task DisconnectAndDisposeAsync(SshClient client)
    {
        try
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
        catch
        {
            // Best-effort.
        }

        client.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static (string Text, bool Truncated) Truncate(string? value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (string.Empty, false);
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount <= maxBytes)
        {
            return (value, false);
        }

        // Tronque en caractères jusqu'à rester sous la limite d'octets.
        var chars = value.AsSpan();
        var length = chars.Length;
        while (length > 0 && Encoding.UTF8.GetByteCount(chars[..length]) > maxBytes)
        {
            length--;
        }

        return (chars[..length].ToString(), true);
    }
}
