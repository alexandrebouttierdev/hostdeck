using System;
using System.Net.Sockets;
using HostDeck.Application.Errors;
using Renci.SshNet.Common;

namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Convertit les exceptions SSH.NET en erreurs Application typées (§11).
/// </summary>
internal static class SshExceptionMapper
{
    public static Exception MapConnectionFailure(
        string host,
        string username,
        Exception exception,
        bool isJumpHost = false,
        string? targetHost = null)
    {
        if (exception is HostKeyVerificationException)
        {
            return exception;
        }

        if (exception is Application.Errors.SshAuthenticationException)
        {
            return exception;
        }

        if (exception is SocketException socket)
        {
            if (isJumpHost && targetHost is not null)
            {
                return new GatewayUnavailableException(host, targetHost, socket);
            }

            return new Application.Errors.SshConnectionException(host, socket.SocketErrorCode.ToString(), socket);
        }

        if (exception is SshOperationTimeoutException or TimeoutException)
        {
            if (isJumpHost && targetHost is not null)
            {
                return new GatewayUnavailableException(host, targetHost, exception);
            }

            return new Application.Errors.SshConnectionException(host, "timeout", exception);
        }

        if (exception is Renci.SshNet.Common.SshAuthenticationException
            || LooksLikeAuthenticationFailure(exception))
        {
            return new Application.Errors.SshAuthenticationException(host, username, exception);
        }

        if (isJumpHost && targetHost is not null)
        {
            return new GatewayUnavailableException(host, targetHost, exception);
        }

        return new Application.Errors.SshConnectionException(host, exception.GetType().Name, exception);
    }

    private static bool LooksLikeAuthenticationFailure(Exception exception)
    {
        var message = exception.Message;
        return message.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase);
    }
}
