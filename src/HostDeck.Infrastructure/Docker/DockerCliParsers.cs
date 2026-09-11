using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HostDeck.Application.Dtos.Docker;
using HostDeck.Application.Errors;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Infrastructure.Docker;

/// <summary>
/// Parse la sortie JSON (ou texte pour les journaux) des commandes Docker CLI.
/// </summary>
internal static class DockerCliParsers
{
    private const string InfoSource = "docker info";
    private const string PsSource = "docker ps";
    private const string InspectSource = "docker inspect";
    private const string StatsSource = "docker stats";

    public static DockerHost ParseInfo(string raw, ServerId serverId)
    {
        ArgumentNullException.ThrowIfNull(raw);

        using var document = ParseRootObject(raw, InfoSource);
        var root = document.RootElement;

        var version = RequireString(root, "ServerVersion", InfoSource);
        var running = RequireInt(root, "ContainersRunning", InfoSource);
        var total = RequireInt(root, "Containers", InfoSource);
        var images = RequireInt(root, "Images", InfoSource);

        return new DockerHost(serverId, version, running, total, images);
    }

    public static IReadOnlyList<DockerContainer> ParseContainerList(string raw, ServerId serverId)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var containers = new List<DockerContainer>();
        foreach (var line in SplitLines(raw))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = ParseRootObject(line, PsSource);
            var root = document.RootElement;

            var id = RequireString(root, "ID", PsSource);
            var name = RequireString(root, "Names", PsSource).TrimStart('/');
            var image = RequireString(root, "Image", PsSource);
            var state = GetStringOrEmpty(root, "State");
            var status = GetStringOrEmpty(root, "Status");
            var portsText = GetStringOrEmpty(root, "Ports");

            containers.Add(new DockerContainer(
                new DockerContainerId(id),
                serverId,
                name,
                image,
                MapContainerStatus(state, status),
                MapHealthFromStatus(status),
                restartCount: 0,
                startedAt: null,
                ports: ParsePorts(portsText)));
        }

        return containers;
    }

    public static DockerContainerDetailsDto ParseInspect(
        string raw,
        ServerId serverId,
        DockerContainerId containerId)
    {
        ArgumentNullException.ThrowIfNull(raw);

        using var document = ParseRootObject(raw, InspectSource);
        var root = document.RootElement;

        var name = GetFirstName(root);
        var image = RequireString(root, "Image", InspectSource);
        var state = root.GetProperty("State");
        var statusText = GetStringOrEmpty(state, "Status");
        var startedAt = ParseNullableDateTime(GetStringOrEmpty(state, "StartedAt"));
        var restartCount = state.TryGetProperty("RestartCount", out var restartElement)
            && restartElement.TryGetInt32(out var restartValue)
            ? restartValue
            : 0;

        var summary = new DockerContainerSummaryDto
        {
            ContainerId = containerId.Value,
            ServerId = serverId.Value,
            Name = name,
            Image = image,
            Status = MapContainerStatus(statusText, statusText),
            Health = MapHealthFromInspectState(state),
            RestartCount = restartCount,
            Uptime = startedAt is { } started && MapContainerStatus(statusText, statusText) == DockerContainerStatus.Running
                ? DateTimeOffset.UtcNow - started.ToUniversalTime()
                : null,
        };

        var command = TryReadCommand(root);
        var ports = ReadPublishedPorts(root);
        var networks = ReadNetworkNames(root);
        var volumes = ReadVolumeNames(root);
        var createdAt = ParseNullableDateTime(GetStringOrEmpty(root, "Created"));

        return new DockerContainerDetailsDto
        {
            Summary = summary,
            Command = command,
            Ports = ports,
            Networks = networks,
            Volumes = volumes,
            CreatedAt = createdAt,
        };
    }

    public static DockerContainerStats ParseStats(string raw, DockerContainerId containerId)
    {
        ArgumentNullException.ThrowIfNull(raw);

        using var document = ParseRootObject(raw, StatsSource);
        var root = document.RootElement;

        var cpu = ParsePercentage(GetStringOrEmpty(root, "CPUPerc"));
        var (memoryUsed, memoryLimit) = ParseMemoryUsage(GetStringOrEmpty(root, "MemUsage"));
        var (networkRx, networkTx) = ParseIoPair(GetStringOrEmpty(root, "NetIO"));
        var (blockRead, blockWrite) = ParseIoPair(GetStringOrEmpty(root, "BlockIO"));

        return new DockerContainerStats(
            containerId,
            DateTimeOffset.UtcNow,
            cpu,
            memoryUsed,
            memoryLimit,
            networkRx,
            networkTx,
            blockRead,
            blockWrite);
    }

    public static IReadOnlyDictionary<string, DockerContainerStatsDto> ParseStatsBatch(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var stats = new Dictionary<string, DockerContainerStatsDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in SplitLines(raw))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = ParseRootObject(line, StatsSource);
            var root = document.RootElement;
            var id = RequireString(root, "ID", StatsSource);
            var containerId = new DockerContainerId(id);
            var domain = ParseStats(line, containerId);

            stats[id] = new DockerContainerStatsDto
            {
                ContainerId = id,
                ObservedAt = domain.ObservedAt,
                CpuPercent = domain.Cpu.Value,
                MemoryUsedBytes = domain.MemoryUsed.Bytes,
                MemoryLimitBytes = domain.MemoryLimit.Bytes,
                MemoryPercent = domain.MemoryRatio.Value,
                NetworkReceivedBytes = domain.NetworkReceived.Bytes,
                NetworkTransmittedBytes = domain.NetworkTransmitted.Bytes,
                BlockReadBytes = domain.BlockRead.Bytes,
                BlockWrittenBytes = domain.BlockWritten.Bytes,
            };
        }

        return stats;
    }

    public static IEnumerable<ContainerLogEntryDto> ParseLogs(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        foreach (var line in SplitLines(raw))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return ParseLogLine(line);
        }
    }

    private static ContainerLogEntryDto ParseLogLine(string line)
    {
        // Format --timestamps : 2024-01-02T15:04:05.999999999Z message
        var spaceIndex = line.IndexOf(' ');
        if (spaceIndex > 0
            && DateTimeOffset.TryParse(
                line.AsSpan(0, spaceIndex),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return new ContainerLogEntryDto(
                timestamp,
                ContainerLogChannel.StandardOutput,
                line[(spaceIndex + 1)..]);
        }

        return new ContainerLogEntryDto(
            DateTimeOffset.UtcNow,
            ContainerLogChannel.StandardOutput,
            line);
    }

    private static JsonDocument ParseRootObject(string raw, string source)
    {
        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException exception)
        {
            throw new DockerException(source, "sortie JSON illisible", exception);
        }
    }

    private static string[] SplitLines(string raw) =>
        raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string RequireString(JsonElement root, string propertyName, string source)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            throw new DockerException(source, $"propriété « {propertyName} » absente ou invalide");
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DockerException(source, $"propriété « {propertyName} » vide");
        }

        return value.Trim();
    }

    private static int RequireInt(JsonElement root, string propertyName, string source)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new DockerException(source, $"propriété « {propertyName} » absente");
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return number;
        }

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        throw new DockerException(source, $"propriété « {propertyName} » invalide");
    }

    private static string GetStringOrEmpty(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;

    private static DockerContainerStatus MapContainerStatus(string state, string status)
    {
        var normalized = (string.IsNullOrWhiteSpace(state) ? status : state).Trim().ToLowerInvariant();

        return normalized switch
        {
            "created" => DockerContainerStatus.Created,
            "running" => DockerContainerStatus.Running,
            "paused" => DockerContainerStatus.Paused,
            "restarting" => DockerContainerStatus.Restarting,
            "removing" => DockerContainerStatus.Removing,
            "exited" or "dead" => DockerContainerStatus.Exited,
            _ when normalized.StartsWith("up ", StringComparison.Ordinal) => DockerContainerStatus.Running,
            _ when normalized.StartsWith("exited", StringComparison.Ordinal) => DockerContainerStatus.Exited,
            _ => DockerContainerStatus.Unknown,
        };
    }

    private static DockerHealthStatus MapHealthFromStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return DockerHealthStatus.None;
        }

        if (status.Contains("(healthy)", StringComparison.OrdinalIgnoreCase))
        {
            return DockerHealthStatus.Healthy;
        }

        if (status.Contains("(unhealthy)", StringComparison.OrdinalIgnoreCase))
        {
            return DockerHealthStatus.Unhealthy;
        }

        if (status.Contains("(health: starting)", StringComparison.OrdinalIgnoreCase)
            || status.Contains("(starting)", StringComparison.OrdinalIgnoreCase))
        {
            return DockerHealthStatus.Starting;
        }

        return DockerHealthStatus.None;
    }

    private static DockerHealthStatus MapHealthFromInspectState(JsonElement state)
    {
        if (!state.TryGetProperty("Health", out var health))
        {
            return DockerHealthStatus.None;
        }

        var status = GetStringOrEmpty(health, "Status");
        return status.ToLowerInvariant() switch
        {
            "healthy" => DockerHealthStatus.Healthy,
            "unhealthy" => DockerHealthStatus.Unhealthy,
            "starting" => DockerHealthStatus.Starting,
            _ => DockerHealthStatus.None,
        };
    }

    private static string[] ParsePorts(string portsText)
    {
        if (string.IsNullOrWhiteSpace(portsText))
        {
            return [];
        }

        return portsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetFirstName(JsonElement root)
    {
        if (root.TryGetProperty("Name", out var singleName) && singleName.ValueKind == JsonValueKind.String)
        {
            return singleName.GetString()!.TrimStart('/');
        }

        if (root.TryGetProperty("Names", out var names) && names.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in names.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    return item.GetString()!.TrimStart('/');
                }
            }
        }

        throw new DockerException(InspectSource, "nom de conteneur absent");
    }

    private static string? TryReadCommand(JsonElement root)
    {
        if (!root.TryGetProperty("Config", out var config))
        {
            return null;
        }

        if (config.TryGetProperty("Cmd", out var cmd) && cmd.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var part in cmd.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                {
                    parts.Add(part.GetString()!);
                }
            }

            return parts.Count == 0 ? null : string.Join(' ', parts);
        }

        return null;
    }

    private static List<string> ReadPublishedPorts(JsonElement root)
    {
        if (!root.TryGetProperty("NetworkSettings", out var settings)
            || !settings.TryGetProperty("Ports", out var ports)
            || ports.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var results = new List<string>();
        foreach (var property in ports.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var binding in property.Value.EnumerateArray())
            {
                if (binding.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var hostIp = GetStringOrEmpty(binding, "HostIp");
                var hostPort = GetStringOrEmpty(binding, "HostPort");
                if (string.IsNullOrWhiteSpace(hostPort))
                {
                    continue;
                }

                results.Add(string.IsNullOrWhiteSpace(hostIp)
                    ? $"{hostPort}->{property.Name}"
                    : $"{hostIp}:{hostPort}->{property.Name}");
            }
        }

        return results;
    }

    private static string[] ReadNetworkNames(JsonElement root)
    {
        if (!root.TryGetProperty("NetworkSettings", out var settings)
            || !settings.TryGetProperty("Networks", out var networks)
            || networks.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return networks
            .EnumerateObject()
            .Select(static network => network.Name)
            .ToArray();
    }

    private static List<string> ReadVolumeNames(JsonElement root)
    {
        if (!root.TryGetProperty("Mounts", out var mounts) || mounts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var names = new List<string>();
        foreach (var mount in mounts.EnumerateArray())
        {
            if (mount.TryGetProperty("Name", out var nameElement)
                && nameElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                names.Add(nameElement.GetString()!.Trim());
            }
        }

        return names;
    }

    private static DateTimeOffset? ParseNullableDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("0001-01-01", StringComparison.Ordinal))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static Percentage ParsePercentage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Percentage.Zero;
        }

        value = value.Trim().TrimEnd('%');
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? Percentage.Clamp(number)
            : Percentage.Zero;
    }

    private static (ByteSize Used, ByteSize Limit) ParseMemoryUsage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (ByteSize.Zero, ByteSize.Zero);
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new DockerException(StatsSource, $"format « MemUsage » inattendu : {value}");
        }

        return (ParseByteSize(parts[0]), ParseByteSize(parts[1]));
    }

    private static (ByteSize First, ByteSize Second) ParseIoPair(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (ByteSize.Zero, ByteSize.Zero);
        }

        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new DockerException(StatsSource, $"format d'E/S inattendu : {value}");
        }

        return (ParseByteSize(parts[0]), ParseByteSize(parts[1]));
    }

    private static ByteSize ParseByteSize(string value)
    {
        value = value.Trim();
        if (value.Equals("0B", StringComparison.OrdinalIgnoreCase))
        {
            return ByteSize.Zero;
        }

        var unitIndex = value.Length - 1;
        while (unitIndex >= 0 && !char.IsDigit(value[unitIndex]))
        {
            unitIndex--;
        }

        if (unitIndex < 0)
        {
            throw new DockerException(StatsSource, $"taille illisible : {value}");
        }

        var numberText = value[..(unitIndex + 1)];
        var unitText = value[(unitIndex + 1)..].Trim();

        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            throw new DockerException(StatsSource, $"taille illisible : {value}");
        }

        var multiplier = unitText.ToUpperInvariant() switch
        {
            "B" => 1d,
            "KB" or "KIB" => 1024d,
            "MB" or "MIB" => 1024d * 1024d,
            "GB" or "GIB" => 1024d * 1024d * 1024d,
            "TB" or "TIB" => 1024d * 1024d * 1024d * 1024d,
            _ => throw new DockerException(StatsSource, $"unité inconnue dans « {value} »"),
        };

        var bytes = (long)Math.Round(number * multiplier, MidpointRounding.AwayFromZero);
        return new ByteSize(bytes);
    }
}
