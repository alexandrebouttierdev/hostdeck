// Package migrate applies versioned SQLite schema migrations.
package migrate

import (
	"context"
	"database/sql"
	"fmt"
	"time"
)

// Migration is a single numbered schema change.
type Migration struct {
	Version int
	Name    string
	SQL     string
}

// Migrations is the ordered list of schema versions.
var Migrations = []Migration{
	{
		Version: 1,
		Name:    "initial_schema",
		SQL: `
CREATE TABLE IF NOT EXISTS servers (
	id TEXT PRIMARY KEY NOT NULL,
	name TEXT NOT NULL,
	host TEXT NOT NULL,
	port INTEGER NOT NULL,
	username TEXT NOT NULL,
	connection_mode TEXT NOT NULL,
	jump_host_id TEXT,
	auth_method TEXT NOT NULL,
	credential_ref TEXT NOT NULL DEFAULT '',
	group_name TEXT NOT NULL DEFAULT '',
	environment TEXT NOT NULL DEFAULT '',
	role TEXT NOT NULL DEFAULT '',
	os_family TEXT NOT NULL DEFAULT '',
	os_name TEXT NOT NULL DEFAULT '',
	status TEXT NOT NULL DEFAULT 'unknown',
	monitoring_enabled INTEGER NOT NULL DEFAULT 1,
	docker_enabled INTEGER NOT NULL DEFAULT 0,
	interval_seconds INTEGER NOT NULL DEFAULT 60,
	created_at TEXT NOT NULL,
	updated_at TEXT NOT NULL,
	last_collected_at TEXT,
	FOREIGN KEY (jump_host_id) REFERENCES servers(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS server_tags (
	server_id TEXT NOT NULL,
	tag TEXT NOT NULL,
	PRIMARY KEY (server_id, tag),
	FOREIGN KEY (server_id) REFERENCES servers(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS metric_samples (
	id TEXT PRIMARY KEY NOT NULL,
	server_id TEXT NOT NULL,
	collected_at TEXT NOT NULL,
	cpu_user_percent REAL NOT NULL DEFAULT 0,
	cpu_system_percent REAL NOT NULL DEFAULT 0,
	cpu_iowait_percent REAL NOT NULL DEFAULT 0,
	cpu_nice_percent REAL NOT NULL DEFAULT 0,
	cpu_steal_percent REAL NOT NULL DEFAULT 0,
	cpu_total_percent REAL NOT NULL DEFAULT 0,
	memory_used_bytes INTEGER NOT NULL DEFAULT 0,
	memory_cache_bytes INTEGER NOT NULL DEFAULT 0,
	memory_buffer_bytes INTEGER NOT NULL DEFAULT 0,
	memory_free_bytes INTEGER NOT NULL DEFAULT 0,
	memory_total_bytes INTEGER NOT NULL DEFAULT 0,
	swap_used_bytes INTEGER NOT NULL DEFAULT 0,
	swap_total_bytes INTEGER NOT NULL DEFAULT 0,
	load1 REAL NOT NULL DEFAULT 0,
	load5 REAL NOT NULL DEFAULT 0,
	load15 REAL NOT NULL DEFAULT 0,
	network_rx_bytes_per_sec REAL NOT NULL DEFAULT 0,
	network_tx_bytes_per_sec REAL NOT NULL DEFAULT 0,
	disk_used_bytes INTEGER NOT NULL DEFAULT 0,
	disk_total_bytes INTEGER NOT NULL DEFAULT 0,
	disk_read_bytes_per_sec REAL NOT NULL DEFAULT 0,
	disk_write_bytes_per_sec REAL NOT NULL DEFAULT 0,
	uptime_seconds INTEGER NOT NULL DEFAULT 0,
	FOREIGN KEY (server_id) REFERENCES servers(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_metric_samples_server_collected
	ON metric_samples (server_id, collected_at);

CREATE TABLE IF NOT EXISTS incidents (
	id TEXT PRIMARY KEY NOT NULL,
	display_id TEXT NOT NULL DEFAULT '',
	server_id TEXT NOT NULL,
	server_name TEXT NOT NULL DEFAULT '',
	metric TEXT NOT NULL DEFAULT '',
	problem TEXT NOT NULL DEFAULT '',
	severity TEXT NOT NULL,
	rule_id TEXT NOT NULL DEFAULT '',
	rule_name TEXT NOT NULL DEFAULT '',
	status TEXT NOT NULL,
	current_value REAL NOT NULL DEFAULT 0,
	threshold REAL NOT NULL DEFAULT 0,
	started_at TEXT NOT NULL,
	last_updated_at TEXT NOT NULL,
	acknowledged_at TEXT,
	acknowledged_by TEXT NOT NULL DEFAULT '',
	recovered_at TEXT,
	resolved_at TEXT,
	notes TEXT NOT NULL DEFAULT '',
	FOREIGN KEY (server_id) REFERENCES servers(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_incidents_status ON incidents (status);
CREATE INDEX IF NOT EXISTS idx_incidents_server ON incidents (server_id);

CREATE TABLE IF NOT EXISTS alert_rules (
	id TEXT PRIMARY KEY NOT NULL,
	name TEXT NOT NULL,
	enabled INTEGER NOT NULL DEFAULT 1,
	metric TEXT NOT NULL,
	operator TEXT NOT NULL DEFAULT '',
	threshold REAL NOT NULL DEFAULT 0,
	duration_seconds INTEGER NOT NULL DEFAULT 300,
	severity TEXT NOT NULL,
	cooldown_seconds INTEGER NOT NULL DEFAULT 0,
	scope TEXT NOT NULL DEFAULT '',
	category TEXT NOT NULL DEFAULT '',
	expression TEXT NOT NULL DEFAULT '',
	last_triggered_at TEXT,
	status TEXT NOT NULL DEFAULT '',
	created_at TEXT NOT NULL,
	updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
	id INTEGER PRIMARY KEY CHECK (id = 1),
	instance_name TEXT NOT NULL DEFAULT '',
	description TEXT NOT NULL DEFAULT '',
	timezone TEXT NOT NULL DEFAULT '',
	language TEXT NOT NULL DEFAULT '',
	auto_start INTEGER NOT NULL DEFAULT 0,
	default_interval_seconds INTEGER NOT NULL DEFAULT 60,
	request_timeout_seconds INTEGER NOT NULL DEFAULT 10,
	retry_attempts INTEGER NOT NULL DEFAULT 3,
	parallel_collection INTEGER NOT NULL DEFAULT 0,
	auto_discovery INTEGER NOT NULL DEFAULT 0,
	availability_check INTEGER NOT NULL DEFAULT 0,
	ping_before_collect INTEGER NOT NULL DEFAULT 0,
	delay_between_hosts_sec INTEGER NOT NULL DEFAULT 0,
	theme TEXT NOT NULL DEFAULT '',
	display_density TEXT NOT NULL DEFAULT '',
	chart_style TEXT NOT NULL DEFAULT '',
	auto_refresh INTEGER NOT NULL DEFAULT 0,
	refresh_interval_seconds INTEGER NOT NULL DEFAULT 30,
	animations_enabled INTEGER NOT NULL DEFAULT 0,
	number_format TEXT NOT NULL DEFAULT '',
	temperature_unit TEXT NOT NULL DEFAULT '',
	auth_required INTEGER NOT NULL DEFAULT 0,
	auto_session INTEGER NOT NULL DEFAULT 0,
	session_duration_hours INTEGER NOT NULL DEFAULT 24,
	default_role TEXT NOT NULL DEFAULT '',
	access_logging INTEGER NOT NULL DEFAULT 0,
	data_encryption INTEGER NOT NULL DEFAULT 0,
	metrics_retention_days INTEGER NOT NULL DEFAULT 90,
	events_retention_days INTEGER NOT NULL DEFAULT 180,
	max_disk_gb INTEGER NOT NULL DEFAULT 100,
	auto_cleanup INTEGER NOT NULL DEFAULT 0,
	compress_metrics INTEGER NOT NULL DEFAULT 0,
	storage_path TEXT NOT NULL DEFAULT '',
	notify_email INTEGER NOT NULL DEFAULT 0,
	notify_slack INTEGER NOT NULL DEFAULT 0,
	notify_teams INTEGER NOT NULL DEFAULT 0,
	notify_webhook INTEGER NOT NULL DEFAULT 0,
	notify_desktop INTEGER NOT NULL DEFAULT 0,
	updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ssh_host_keys (
	host TEXT NOT NULL,
	port INTEGER NOT NULL,
	key_type TEXT NOT NULL,
	fingerprint TEXT NOT NULL,
	public_key TEXT NOT NULL DEFAULT '',
	first_seen_at TEXT NOT NULL,
	last_seen_at TEXT NOT NULL,
	PRIMARY KEY (host, port, key_type)
);
`,
	},
}

// Up applies all pending migrations in order.
func Up(ctx context.Context, db *sql.DB) error {
	if _, err := db.ExecContext(ctx, `
CREATE TABLE IF NOT EXISTS schema_migrations (
	version INTEGER PRIMARY KEY NOT NULL,
	name TEXT NOT NULL,
	applied_at TEXT NOT NULL
)`); err != nil {
		return fmt.Errorf("create schema_migrations: %w", err)
	}

	for _, m := range Migrations {
		var exists int
		err := db.QueryRowContext(ctx,
			`SELECT 1 FROM schema_migrations WHERE version = ?`, m.Version,
		).Scan(&exists)
		if err == nil {
			continue
		}
		if err != sql.ErrNoRows {
			return fmt.Errorf("check migration %d: %w", m.Version, err)
		}

		tx, err := db.BeginTx(ctx, nil)
		if err != nil {
			return fmt.Errorf("begin migration %d: %w", m.Version, err)
		}

		if _, err := tx.ExecContext(ctx, m.SQL); err != nil {
			_ = tx.Rollback()
			return fmt.Errorf("apply migration %d (%s): %w", m.Version, m.Name, err)
		}

		if _, err := tx.ExecContext(ctx,
			`INSERT INTO schema_migrations (version, name, applied_at) VALUES (?, ?, ?)`,
			m.Version, m.Name, time.Now().UTC().Format(time.RFC3339Nano),
		); err != nil {
			_ = tx.Rollback()
			return fmt.Errorf("record migration %d: %w", m.Version, err)
		}

		if err := tx.Commit(); err != nil {
			return fmt.Errorf("commit migration %d: %w", m.Version, err)
		}
	}

	return nil
}

// CurrentVersion returns the highest applied migration version, or 0 if none.
func CurrentVersion(ctx context.Context, db *sql.DB) (int, error) {
	var version sql.NullInt64
	err := db.QueryRowContext(ctx, `SELECT MAX(version) FROM schema_migrations`).Scan(&version)
	if err != nil {
		return 0, err
	}
	if !version.Valid {
		return 0, nil
	}
	return int(version.Int64), nil
}
