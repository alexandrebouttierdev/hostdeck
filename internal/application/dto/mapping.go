package dto

import (
	"time"

	dockerdomain "github.com/alexandrebouttierdev/hostdeck/internal/domain/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// FromServer maps a domain server to a summary DTO (metrics filled separately).
func FromServer(srv server.Server) ServerSummaryDTO {
	jump := ""
	if srv.JumpHostID != nil {
		jump = srv.JumpHostID.String()
	}
	return ServerSummaryDTO{
		ID:                srv.ID.String(),
		Name:              srv.Name,
		Host:              srv.Host,
		Port:              srv.Port,
		OSName:            srv.OSName,
		OSFamily:          srv.OSFamily,
		Group:             srv.Group,
		Environment:       srv.Environment,
		Role:              srv.Role,
		Tags:              append([]string(nil), srv.Tags...),
		Status:            string(srv.Status),
		MonitoringEnabled: srv.MonitoringEnabled,
		DockerEnabled:     srv.DockerEnabled,
		ConnectionMode:    string(srv.ConnectionMode),
		JumpHostID:        jump,
		LastCollected:     srv.LastCollectedAt,
	}
}

// FromServerDetails maps server + optional latest metrics.
func FromServerDetails(srv server.Server, latest *monitoring.MetricSample) ServerDetailsDTO {
	summary := FromServer(srv)
	details := ServerDetailsDTO{
		ServerSummaryDTO: summary,
		Username:         srv.Username,
		AuthMethod:       string(srv.AuthMethod),
		CredentialRef:    srv.CredentialRef,
		IntervalSeconds:  srv.IntervalSeconds,
		CreatedAt:        srv.CreatedAt,
		UpdatedAt:        srv.UpdatedAt,
	}
	if latest != nil {
		m := FromMetricSample(*latest)
		details.LatestMetrics = &m
		ApplyMetricsToSummary(&details.ServerSummaryDTO, *latest)
	}
	return details
}

// ApplyMetricsToSummary fills current metric fields on a summary row.
func ApplyMetricsToSummary(dst *ServerSummaryDTO, sample monitoring.MetricSample) {
	dst.CPUPercent = sample.CPUTotalPercent
	dst.MemoryPercent = sample.MemoryUsedPercent()
	dst.DiskPercent = sample.DiskUsedPercent()
	dst.Load1 = sample.Load1
	dst.NetworkRXMbps = sample.NetworkRXBytesPerSec * 8 / 1_000_000
	dst.NetworkTXMbps = sample.NetworkTXBytesPerSec * 8 / 1_000_000
	dst.UptimeSeconds = sample.UptimeSeconds
	t := sample.CollectedAt
	dst.LastCollected = &t
}

// FromMetricSample maps a domain sample to LatestMetricDTO.
func FromMetricSample(sample monitoring.MetricSample) LatestMetricDTO {
	return LatestMetricDTO{
		ServerID:             sample.ServerID.String(),
		CollectedAt:          sample.CollectedAt,
		CPUUserPercent:       sample.CPUUserPercent,
		CPUSystemPercent:     sample.CPUSystemPercent,
		CPUIOWaitPercent:     sample.CPUIOWaitPercent,
		CPUNicePercent:       sample.CPUNicePercent,
		CPUStealPercent:      sample.CPUStealPercent,
		CPUTotalPercent:      sample.CPUTotalPercent,
		MemoryUsedBytes:      sample.MemoryUsedBytes,
		MemoryCacheBytes:     sample.MemoryCacheBytes,
		MemoryBufferBytes:    sample.MemoryBufferBytes,
		MemoryFreeBytes:      sample.MemoryFreeBytes,
		MemoryTotalBytes:     sample.MemoryTotalBytes,
		MemoryUsedPercent:    sample.MemoryUsedPercent(),
		SwapUsedBytes:        sample.SwapUsedBytes,
		SwapTotalBytes:       sample.SwapTotalBytes,
		SwapUsedPercent:      sample.SwapUsedPercent(),
		Load1:                sample.Load1,
		Load5:                sample.Load5,
		Load15:               sample.Load15,
		NetworkRXBytesPerSec: sample.NetworkRXBytesPerSec,
		NetworkTXBytesPerSec: sample.NetworkTXBytesPerSec,
		DiskUsedBytes:        sample.DiskUsedBytes,
		DiskTotalBytes:       sample.DiskTotalBytes,
		DiskUsedPercent:      sample.DiskUsedPercent(),
		DiskReadBytesPerSec:  sample.DiskReadBytesPerSec,
		DiskWriteBytesPerSec: sample.DiskWriteBytesPerSec,
		UptimeSeconds:        sample.UptimeSeconds,
	}
}

// FromMetricHistory maps samples to history DTO.
func FromMetricHistory(serverID string, from, to time.Time, samples []monitoring.MetricSample) MetricHistoryDTO {
	points := make([]MetricPointDTO, 0, len(samples))
	for _, s := range samples {
		points = append(points, MetricPointDTO{
			CollectedAt:          s.CollectedAt,
			CPUTotalPercent:      s.CPUTotalPercent,
			MemoryUsedPercent:    s.MemoryUsedPercent(),
			DiskUsedPercent:      s.DiskUsedPercent(),
			Load1:                s.Load1,
			NetworkRXBytesPerSec: s.NetworkRXBytesPerSec,
			NetworkTXBytesPerSec: s.NetworkTXBytesPerSec,
		})
	}
	return MetricHistoryDTO{ServerID: serverID, From: from, To: to, Points: points}
}

// ComputeMetricStatistics aggregates history points.
func ComputeMetricStatistics(serverID string, samples []monitoring.MetricSample) MetricStatisticsDTO {
	stats := MetricStatisticsDTO{ServerID: serverID}
	if len(samples) == 0 {
		return stats
	}
	var cpuSum, memSum, diskSum, loadSum float64
	for _, s := range samples {
		cpu := s.CPUTotalPercent
		mem := s.MemoryUsedPercent()
		disk := s.DiskUsedPercent()
		load := s.Load1
		cpuSum += cpu
		memSum += mem
		diskSum += disk
		loadSum += load
		if cpu > stats.CPUPeak {
			stats.CPUPeak = cpu
		}
		if mem > stats.MemoryPeak {
			stats.MemoryPeak = mem
		}
		if disk > stats.DiskPeak {
			stats.DiskPeak = disk
		}
		if load > stats.LoadPeak {
			stats.LoadPeak = load
		}
	}
	n := float64(len(samples))
	stats.CPUAvg = cpuSum / n
	stats.MemoryAvg = memSum / n
	stats.DiskAvg = diskSum / n
	stats.LoadAvg = loadSum / n
	return stats
}

// FromIncident maps a domain incident to IncidentDTO.
func FromIncident(inc incident.Incident) IncidentDTO {
	return IncidentDTO{
		ID:             inc.ID.String(),
		DisplayID:      inc.DisplayID,
		ServerID:       inc.ServerID.String(),
		ServerName:     inc.ServerName,
		Metric:         inc.Metric,
		Problem:        inc.Problem,
		Severity:       string(inc.Severity),
		RuleID:         inc.RuleID,
		RuleName:       inc.RuleName,
		Status:         string(inc.Status),
		CurrentValue:   inc.CurrentValue,
		Threshold:      inc.Threshold,
		StartedAt:      inc.StartedAt,
		LastUpdatedAt:  inc.LastUpdatedAt,
		DurationSecs:   int64(inc.Duration().Seconds()),
		AcknowledgedAt: inc.AcknowledgedAt,
		AcknowledgedBy: inc.AcknowledgedBy,
		RecoveredAt:    inc.RecoveredAt,
		ResolvedAt:     inc.ResolvedAt,
	}
}

// FromIncidentDetails maps incident details.
func FromIncidentDetails(inc incident.Incident) IncidentDetailsDTO {
	return IncidentDetailsDTO{
		IncidentDTO: FromIncident(inc),
		Notes:       inc.Notes,
	}
}

// FromAlertRule maps an alert rule.
func FromAlertRule(rule incident.AlertRule) AlertRuleDTO {
	return AlertRuleDTO{
		ID:              rule.ID,
		Name:            rule.Name,
		Enabled:         rule.Enabled,
		Metric:          rule.Metric,
		Operator:        rule.Operator,
		Threshold:       rule.Threshold,
		DurationSeconds: rule.DurationSeconds,
		Severity:        string(rule.Severity),
		CooldownSeconds: rule.CooldownSeconds,
		Scope:           rule.Scope,
		Category:        rule.Category,
		Expression:      rule.Expression,
		LastTriggeredAt: rule.LastTriggeredAt,
		Status:          rule.Status,
		CreatedAt:       rule.CreatedAt,
		UpdatedAt:       rule.UpdatedAt,
	}
}

// ToAlertRule maps DTO to domain (generates ID if empty).
func ToAlertRule(d AlertRuleDTO) incident.AlertRule {
	id := d.ID
	if id == "" {
		id = shared.NewID()
	}
	now := time.Now().UTC()
	created := d.CreatedAt
	if created.IsZero() {
		created = now
	}
	updated := d.UpdatedAt
	if updated.IsZero() {
		updated = now
	}
	return incident.AlertRule{
		ID:              id,
		Name:            d.Name,
		Enabled:         d.Enabled,
		Metric:          d.Metric,
		Operator:        d.Operator,
		Threshold:       d.Threshold,
		DurationSeconds: d.DurationSeconds,
		Severity:        incident.Severity(d.Severity),
		CooldownSeconds: d.CooldownSeconds,
		Scope:           d.Scope,
		Category:        d.Category,
		Expression:      d.Expression,
		LastTriggeredAt: d.LastTriggeredAt,
		Status:          d.Status,
		CreatedAt:       created,
		UpdatedAt:       updated,
	}
}

// FromSettings maps settings.
func FromSettings(s shared.Settings) SettingsDTO {
	return SettingsDTO{
		InstanceName:           s.InstanceName,
		Description:            s.Description,
		Timezone:               s.Timezone,
		Language:               s.Language,
		AutoStart:              s.AutoStart,
		DefaultIntervalSeconds: s.DefaultIntervalSeconds,
		RequestTimeoutSeconds:  s.RequestTimeoutSeconds,
		RetryAttempts:          s.RetryAttempts,
		ParallelCollection:     s.ParallelCollection,
		AutoDiscovery:          s.AutoDiscovery,
		AvailabilityCheck:      s.AvailabilityCheck,
		PingBeforeCollect:      s.PingBeforeCollect,
		DelayBetweenHostsSec:   s.DelayBetweenHostsSec,
		Theme:                  s.Theme,
		DisplayDensity:         s.DisplayDensity,
		ChartStyle:             s.ChartStyle,
		AutoRefresh:            s.AutoRefresh,
		RefreshIntervalSeconds: s.RefreshIntervalSeconds,
		AnimationsEnabled:      s.AnimationsEnabled,
		NumberFormat:           s.NumberFormat,
		TemperatureUnit:        s.TemperatureUnit,
		AuthRequired:           s.AuthRequired,
		AutoSession:            s.AutoSession,
		SessionDurationHours:   s.SessionDurationHours,
		DefaultRole:            s.DefaultRole,
		AccessLogging:          s.AccessLogging,
		DataEncryption:         s.DataEncryption,
		MetricsRetentionDays:   s.MetricsRetentionDays,
		EventsRetentionDays:    s.EventsRetentionDays,
		MaxDiskGB:              s.MaxDiskGB,
		AutoCleanup:            s.AutoCleanup,
		CompressMetrics:        s.CompressMetrics,
		StoragePath:            s.StoragePath,
		NotifyEmail:            s.NotifyEmail,
		NotifySlack:            s.NotifySlack,
		NotifyTeams:            s.NotifyTeams,
		NotifyWebhook:          s.NotifyWebhook,
		NotifyDesktop:          s.NotifyDesktop,
		UpdatedAt:              s.UpdatedAt,
	}
}

// ToSettings maps DTO to domain.
func ToSettings(d SettingsDTO) shared.Settings {
	return shared.Settings{
		InstanceName:           d.InstanceName,
		Description:            d.Description,
		Timezone:               d.Timezone,
		Language:               d.Language,
		AutoStart:              d.AutoStart,
		DefaultIntervalSeconds: d.DefaultIntervalSeconds,
		RequestTimeoutSeconds:  d.RequestTimeoutSeconds,
		RetryAttempts:          d.RetryAttempts,
		ParallelCollection:     d.ParallelCollection,
		AutoDiscovery:          d.AutoDiscovery,
		AvailabilityCheck:      d.AvailabilityCheck,
		PingBeforeCollect:      d.PingBeforeCollect,
		DelayBetweenHostsSec:   d.DelayBetweenHostsSec,
		Theme:                  d.Theme,
		DisplayDensity:         d.DisplayDensity,
		ChartStyle:             d.ChartStyle,
		AutoRefresh:            d.AutoRefresh,
		RefreshIntervalSeconds: d.RefreshIntervalSeconds,
		AnimationsEnabled:      d.AnimationsEnabled,
		NumberFormat:           d.NumberFormat,
		TemperatureUnit:        d.TemperatureUnit,
		AuthRequired:           d.AuthRequired,
		AutoSession:            d.AutoSession,
		SessionDurationHours:   d.SessionDurationHours,
		DefaultRole:            d.DefaultRole,
		AccessLogging:          d.AccessLogging,
		DataEncryption:         d.DataEncryption,
		MetricsRetentionDays:   d.MetricsRetentionDays,
		EventsRetentionDays:    d.EventsRetentionDays,
		MaxDiskGB:              d.MaxDiskGB,
		AutoCleanup:            d.AutoCleanup,
		CompressMetrics:        d.CompressMetrics,
		StoragePath:            d.StoragePath,
		NotifyEmail:            d.NotifyEmail,
		NotifySlack:            d.NotifySlack,
		NotifyTeams:            d.NotifyTeams,
		NotifyWebhook:          d.NotifyWebhook,
		NotifyDesktop:          d.NotifyDesktop,
		UpdatedAt:              d.UpdatedAt,
	}
}

// FromContainer maps a container to summary DTO.
func FromContainer(c dockerdomain.Container) DockerContainerSummaryDTO {
	return DockerContainerSummaryDTO{
		ID:            c.ID.String(),
		ServerID:      c.ServerID.String(),
		Name:          c.Name,
		Image:         c.Image,
		Status:        string(c.Status),
		State:         c.State,
		Ports:         c.Ports,
		CPUPercent:    c.CPUPercent,
		MemoryUsed:    c.MemoryUsed,
		MemoryLimit:   c.MemoryLimit,
		MemoryPercent: c.MemoryPercent(),
		RestartCount:  c.RestartCount,
	}
}

// FromContainerDetails maps full container details.
func FromContainerDetails(c dockerdomain.Container) DockerContainerDetailsDTO {
	return DockerContainerDetailsDTO{
		DockerContainerSummaryDTO: FromContainer(c),
		Networks:                  c.Networks,
		Command:                   c.Command,
		CreatedAt:                 c.CreatedAt,
		StartedAt:                 c.StartedAt,
		NetworkRX:                 c.NetworkRX,
		NetworkTX:                 c.NetworkTX,
		BlockRead:                 c.BlockRead,
		BlockWrite:                c.BlockWrite,
	}
}

// FromDockerHostInfo maps host info.
func FromDockerHostInfo(info dockerdomain.HostInfo) DockerHostInfoDTO {
	return DockerHostInfoDTO{
		ServerID:          info.ServerID.String(),
		DockerVersion:     info.DockerVersion,
		APIVersion:        info.APIVersion,
		OS:                info.OS,
		Architecture:      info.Architecture,
		Containers:        info.Containers,
		ContainersRunning: info.ContainersRunning,
		Images:            info.Images,
		NCPU:              info.NCPU,
		MemTotal:          info.MemTotal,
	}
}
