package sqlite

import (
	"context"
	"database/sql"
	"errors"
	"fmt"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// SettingsRepository persists global settings as a single typed row.
type SettingsRepository struct {
	db *DB
}

// NewSettingsRepository constructs a SettingsRepository.
func NewSettingsRepository(db *DB) *SettingsRepository {
	return &SettingsRepository{db: db}
}

const settingsColumns = `
	instance_name, description, timezone, language, auto_start,
	default_interval_seconds, request_timeout_seconds, retry_attempts,
	parallel_collection, auto_discovery, availability_check, ping_before_collect,
	delay_between_hosts_sec, theme, display_density, chart_style, auto_refresh,
	refresh_interval_seconds, animations_enabled, number_format, temperature_unit,
	auth_required, auto_session, session_duration_hours, default_role,
	access_logging, data_encryption, metrics_retention_days, events_retention_days,
	max_disk_gb, auto_cleanup, compress_metrics, storage_path,
	notify_email, notify_slack, notify_teams, notify_webhook, notify_desktop, updated_at`

func (r *SettingsRepository) Get(ctx context.Context) (shared.Settings, error) {
	row := r.db.sql.QueryRowContext(ctx,
		`SELECT `+settingsColumns+` FROM settings WHERE id = 1`)
	s, err := scanSettings(row)
	if errors.Is(err, sql.ErrNoRows) {
		return shared.DefaultSettings(), nil
	}
	if err != nil {
		return shared.Settings{}, fmt.Errorf("get settings: %w", err)
	}
	return s, nil
}

func (r *SettingsRepository) Save(ctx context.Context, settings shared.Settings) error {
	_, err := r.db.sql.ExecContext(ctx, `
INSERT INTO settings (id, `+settingsColumns+`)
VALUES (1, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(id) DO UPDATE SET
	instance_name = excluded.instance_name,
	description = excluded.description,
	timezone = excluded.timezone,
	language = excluded.language,
	auto_start = excluded.auto_start,
	default_interval_seconds = excluded.default_interval_seconds,
	request_timeout_seconds = excluded.request_timeout_seconds,
	retry_attempts = excluded.retry_attempts,
	parallel_collection = excluded.parallel_collection,
	auto_discovery = excluded.auto_discovery,
	availability_check = excluded.availability_check,
	ping_before_collect = excluded.ping_before_collect,
	delay_between_hosts_sec = excluded.delay_between_hosts_sec,
	theme = excluded.theme,
	display_density = excluded.display_density,
	chart_style = excluded.chart_style,
	auto_refresh = excluded.auto_refresh,
	refresh_interval_seconds = excluded.refresh_interval_seconds,
	animations_enabled = excluded.animations_enabled,
	number_format = excluded.number_format,
	temperature_unit = excluded.temperature_unit,
	auth_required = excluded.auth_required,
	auto_session = excluded.auto_session,
	session_duration_hours = excluded.session_duration_hours,
	default_role = excluded.default_role,
	access_logging = excluded.access_logging,
	data_encryption = excluded.data_encryption,
	metrics_retention_days = excluded.metrics_retention_days,
	events_retention_days = excluded.events_retention_days,
	max_disk_gb = excluded.max_disk_gb,
	auto_cleanup = excluded.auto_cleanup,
	compress_metrics = excluded.compress_metrics,
	storage_path = excluded.storage_path,
	notify_email = excluded.notify_email,
	notify_slack = excluded.notify_slack,
	notify_teams = excluded.notify_teams,
	notify_webhook = excluded.notify_webhook,
	notify_desktop = excluded.notify_desktop,
	updated_at = excluded.updated_at`,
		settings.InstanceName,
		settings.Description,
		settings.Timezone,
		settings.Language,
		boolToInt(settings.AutoStart),
		settings.DefaultIntervalSeconds,
		settings.RequestTimeoutSeconds,
		settings.RetryAttempts,
		boolToInt(settings.ParallelCollection),
		boolToInt(settings.AutoDiscovery),
		boolToInt(settings.AvailabilityCheck),
		boolToInt(settings.PingBeforeCollect),
		settings.DelayBetweenHostsSec,
		settings.Theme,
		settings.DisplayDensity,
		settings.ChartStyle,
		boolToInt(settings.AutoRefresh),
		settings.RefreshIntervalSeconds,
		boolToInt(settings.AnimationsEnabled),
		settings.NumberFormat,
		settings.TemperatureUnit,
		boolToInt(settings.AuthRequired),
		boolToInt(settings.AutoSession),
		settings.SessionDurationHours,
		settings.DefaultRole,
		boolToInt(settings.AccessLogging),
		boolToInt(settings.DataEncryption),
		settings.MetricsRetentionDays,
		settings.EventsRetentionDays,
		settings.MaxDiskGB,
		boolToInt(settings.AutoCleanup),
		boolToInt(settings.CompressMetrics),
		settings.StoragePath,
		boolToInt(settings.NotifyEmail),
		boolToInt(settings.NotifySlack),
		boolToInt(settings.NotifyTeams),
		boolToInt(settings.NotifyWebhook),
		boolToInt(settings.NotifyDesktop),
		formatTime(settings.UpdatedAt),
	)
	if err != nil {
		return fmt.Errorf("save settings: %w", err)
	}
	return nil
}

func scanSettings(row scannable) (shared.Settings, error) {
	var (
		s                                        shared.Settings
		autoStart, parallelCollection            int
		autoDiscovery, availabilityCheck         int
		pingBeforeCollect, autoRefresh           int
		animationsEnabled, authRequired          int
		autoSession, accessLogging               int
		dataEncryption, autoCleanup              int
		compressMetrics                          int
		notifyEmail, notifySlack, notifyTeams    int
		notifyWebhook, notifyDesktop             int
		updatedAt                                string
	)
	err := row.Scan(
		&s.InstanceName, &s.Description, &s.Timezone, &s.Language, &autoStart,
		&s.DefaultIntervalSeconds, &s.RequestTimeoutSeconds, &s.RetryAttempts,
		&parallelCollection, &autoDiscovery, &availabilityCheck, &pingBeforeCollect,
		&s.DelayBetweenHostsSec, &s.Theme, &s.DisplayDensity, &s.ChartStyle, &autoRefresh,
		&s.RefreshIntervalSeconds, &animationsEnabled, &s.NumberFormat, &s.TemperatureUnit,
		&authRequired, &autoSession, &s.SessionDurationHours, &s.DefaultRole,
		&accessLogging, &dataEncryption, &s.MetricsRetentionDays, &s.EventsRetentionDays,
		&s.MaxDiskGB, &autoCleanup, &compressMetrics, &s.StoragePath,
		&notifyEmail, &notifySlack, &notifyTeams, &notifyWebhook, &notifyDesktop, &updatedAt,
	)
	if err != nil {
		return shared.Settings{}, err
	}

	t, err := parseTime(updatedAt)
	if err != nil {
		return shared.Settings{}, err
	}

	s.AutoStart = intToBool(autoStart)
	s.ParallelCollection = intToBool(parallelCollection)
	s.AutoDiscovery = intToBool(autoDiscovery)
	s.AvailabilityCheck = intToBool(availabilityCheck)
	s.PingBeforeCollect = intToBool(pingBeforeCollect)
	s.AutoRefresh = intToBool(autoRefresh)
	s.AnimationsEnabled = intToBool(animationsEnabled)
	s.AuthRequired = intToBool(authRequired)
	s.AutoSession = intToBool(autoSession)
	s.AccessLogging = intToBool(accessLogging)
	s.DataEncryption = intToBool(dataEncryption)
	s.AutoCleanup = intToBool(autoCleanup)
	s.CompressMetrics = intToBool(compressMetrics)
	s.NotifyEmail = intToBool(notifyEmail)
	s.NotifySlack = intToBool(notifySlack)
	s.NotifyTeams = intToBool(notifyTeams)
	s.NotifyWebhook = intToBool(notifyWebhook)
	s.NotifyDesktop = intToBool(notifyDesktop)
	s.UpdatedAt = t
	return s, nil
}
