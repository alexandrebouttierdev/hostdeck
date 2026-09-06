using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Tests.Fakes;

/// <summary>
/// Trousseau en mémoire.
///
/// Enregistre les secrets écrits afin que les tests puissent vérifier qu'un secret est bien
/// remis au trousseau — et retiré quand l'écriture en base échoue.
/// </summary>
internal sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _secrets = [];

    public Exception? FailNextWriteWith { get; set; }

    public Exception? FailNextDeleteWith { get; set; }

    public bool Available { get; set; } = true;

    public IReadOnlyDictionary<string, string> Secrets => _secrets;

    public int DeleteCalls { get; private set; }

    public Task<SecretMaterial> ReadAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_secrets.TryGetValue(reference.Key, out var secret))
        {
            throw new CredentialException(
                $"Credential '{reference.Key}' not found",
                CredentialFailure.NotFound);
        }

        return Task.FromResult(new SecretMaterial(secret));
    }

    public Task WriteAsync(
        CredentialReference reference,
        ReadOnlyMemory<char> secret,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (FailNextWriteWith is { } exception)
        {
            FailNextWriteWith = null;
            throw exception;
        }

        _secrets[reference.Key] = secret.ToString();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCalls++;

        if (FailNextDeleteWith is { } exception)
        {
            FailNextDeleteWith = null;
            throw exception;
        }

        _secrets.Remove(reference.Key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_secrets.ContainsKey(reference.Key));
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Available);
    }
}

/// <summary>
/// Horloge pilotée par le test.
///
/// Indispensable pour éprouver durées, cooldowns et fenêtres de confirmation sans faire
/// dormir la suite de tests.
/// </summary>
internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

/// <summary>Bus d'événements qui conserve tout ce qui est publié.</summary>
internal sealed class RecordingEventBus : IMonitoringEventBus
{
    private readonly List<MonitoringEvent> _published = [];

    public IReadOnlyList<MonitoringEvent> Published => _published;

    public void Publish(MonitoringEvent monitoringEvent) => _published.Add(monitoringEvent);

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : MonitoringEvent => NullSubscription.Instance;

    private sealed class NullSubscription : IDisposable
    {
        public static readonly NullSubscription Instance = new();

        public void Dispose()
        {
        }
    }
}
