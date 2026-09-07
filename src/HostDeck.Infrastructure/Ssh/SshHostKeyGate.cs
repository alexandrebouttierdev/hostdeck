using System;
using HostDeck.Application.Errors;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Attache la vérification de clé d'hôte à un client SSH.NET.
///
/// <para>
/// La vérification n'est jamais désactivée (§11, §51 T3) : si aucune empreinte n'est
/// approuvée, ou si l'empreinte présentée diffère, <see cref="HostKeyEventArgs.CanTrust"/>
/// reste faux et la connexion échoue. La décision d'approuver appartient à l'opérateur,
/// jamais à cet adaptateur.
/// </para>
/// </summary>
internal sealed class SshHostKeyGate
{
    private string? _presentedFingerprint;
    private HostKeyVerificationException? _rejection;

    public string? PresentedFingerprint => _presentedFingerprint;

    public HostKeyVerificationException? Rejection => _rejection;

    /// <summary>
    /// Branche le gestionnaire sur le client. <paramref name="logicalHost"/> est l'hôte
    /// métier (cible ou bastion), pas l'adresse locale d'un tunnel.
    /// </summary>
    public void Attach(BaseClient client, string logicalHost, int port, string? knownFingerprint)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalHost);

        client.HostKeyReceived += (_, args) =>
        {
            Apply(logicalHost, port, knownFingerprint, args.FingerPrintSHA256, trust => args.CanTrust = trust);
        };
    }

    /// <summary>
    /// Décision pure, testable sans ouvrir de socket.
    /// </summary>
    internal void Apply(
        string logicalHost,
        int port,
        string? knownFingerprint,
        string presentedFingerprint,
        Action<bool> setCanTrust)
    {
        _presentedFingerprint = presentedFingerprint;

        if (knownFingerprint is null)
        {
            _rejection = new HostKeyVerificationException(
                FormatEndpoint(logicalHost, port),
                presentedFingerprint,
                knownFingerprint: null);
            setCanTrust(false);
            return;
        }

        if (!string.Equals(knownFingerprint, presentedFingerprint, StringComparison.Ordinal))
        {
            _rejection = new HostKeyVerificationException(
                FormatEndpoint(logicalHost, port),
                presentedFingerprint,
                knownFingerprint);
            setCanTrust(false);
            return;
        }

        setCanTrust(true);
    }

    /// <summary>
    /// Relève le rejet capturé pendant la négociation, s'il y en a un.
    /// </summary>
    public void ThrowIfRejected()
    {
        if (_rejection is not null)
        {
            throw _rejection;
        }
    }

    private static string FormatEndpoint(string host, int port) => $"{host}:{port}";
}
