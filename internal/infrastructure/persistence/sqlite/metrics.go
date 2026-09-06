package sqlite

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/monitoring"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// MetricsRepository persists metric samples in SQLite.
type MetricsRepository struct {
	db *DB
}

// NewMetricsRepository constructs a MetricsRepository.
func NewMetricsRepository(db *DB) *MetricsRepository {
	return &MetricsRepository{db: db}
}

const metricColumns = `
	id, server_id, collected_at,
	cpu_user_percent, cpu_system_percent, cpu_iowait_percent, cpu_nice_percent, cpu_steal_percent, cpu_total_percent,
	memory_used_bytes, memory_cache_bytes, memory_buffer_bytes, memory_free_bytes, memory_total_bytes,
	swap_used_bytes, swap_total_bytes,
	load1, load5, load15,
	network_rx_bytes_per_sec, network_tx_bytes_per_sec,
	disk_used_bytes, disk_total_bytes, disk_read_bytes_per_sec, disk_write_bytes_per_sec,
	uptime_seconds`

func (r *MetricsRepository) SaveSample(ctx context.Context, sample monitoring.MetricSample) error {
	if sample.ID == "" {
		sample.ID = shared.NewID()
	}
	_, err := r.db.sql.ExecContext(ctx, `
INSERT INTO metric_samples (`+metricColumns+`)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		sample.ID,
		sample.ServerID.String(),
		formatTime(sample.CollectedAt),
		sample.CPUUserPercent,
		sample.CPUSystemPercent,
		sample.CPUIOWaitPercent,
		sample.CPUNicePercent,
		sample.CPUStealPercent,
		sample.CPUTotalPercent,
		sample.MemoryUsedBytes,
		sample.MemoryCacheBytes,
		sample.MemoryBufferBytes,
		sample.MemoryFreeBytes,
		sample.MemoryTotalBytes,
		sample.SwapUsedBytes,
		sample.SwapTotalBytes,
		sample.Load1,
		sample.Load5,
		sample.Load15,
		sample.NetworkRXBytesPerSec,
		sample.NetworkTXBytesPerSec,
		sample.DiskUsedBytes,
		sample.DiskTotalBytes,
		sample.DiskReadBytesPerSec,
		sample.DiskWriteBytesPerSec,
		sample.UptimeSeconds,
	)
	if err != nil {
		return fmt.Errorf("save metric sample: %w", err)
	}
	return nil
}

func (r *MetricsRepository) Latest(ctx context.Context, serverID server.ServerID) (monitoring.MetricSample, error) {
	row := r.db.sql.QueryRowContext(ctx, `
SELECT `+metricColumns+`
FROM metric_samples
WHERE server_id = ?
ORDER BY collected_at DESC
LIMIT 1`, serverID.String())

	sample, err := scanMetric(row)
	if errors.Is(err, sql.ErrNoRows) {
		return monitoring.MetricSample{}, shared.ErrNotFound
	}
	if err != nil {
		return monitoring.MetricSample{}, fmt.Errorf("latest metric: %w", err)
	}
	return sample, nil
}

func (r *MetricsRepository) History(ctx context.Context, serverID server.ServerID, from, to time.Time, limit int) ([]monitoring.MetricSample, error) {
	query := `
SELECT ` + metricColumns + `
FROM metric_samples
WHERE server_id = ? AND collected_at >= ? AND collected_at <= ?
ORDER BY collected_at ASC`
	args := []any{serverID.String(), formatTime(from), formatTime(to)}
	if limit > 0 {
		query += ` LIMIT ?`
		args = append(args, limit)
	}

	rows, err := r.db.sql.QueryContext(ctx, query, args...)
	if err != nil {
		return nil, fmt.Errorf("metric history: %w", err)
	}
	defer rows.Close()

	var out []monitoring.MetricSample
	for rows.Next() {
		sample, err := scanMetric(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, sample)
	}
	return out, rows.Err()
}

func (r *MetricsRepository) PruneBefore(ctx context.Context, before time.Time) (int64, error) {
	res, err := r.db.sql.ExecContext(ctx,
		`DELETE FROM metric_samples WHERE collected_at < ?`, formatTime(before))
	if err != nil {
		return 0, fmt.Errorf("prune metrics: %w", err)
	}
	return res.RowsAffected()
}

func scanMetric(row scannable) (monitoring.MetricSample, error) {
	var (
		id, serverID, collectedAt string
		sample                    monitoring.MetricSample
	)
	err := row.Scan(
		&id, &serverID, &collectedAt,
		&sample.CPUUserPercent, &sample.CPUSystemPercent, &sample.CPUIOWaitPercent,
		&sample.CPUNicePercent, &sample.CPUStealPercent, &sample.CPUTotalPercent,
		&sample.MemoryUsedBytes, &sample.MemoryCacheBytes, &sample.MemoryBufferBytes,
		&sample.MemoryFreeBytes, &sample.MemoryTotalBytes,
		&sample.SwapUsedBytes, &sample.SwapTotalBytes,
		&sample.Load1, &sample.Load5, &sample.Load15,
		&sample.NetworkRXBytesPerSec, &sample.NetworkTXBytesPerSec,
		&sample.DiskUsedBytes, &sample.DiskTotalBytes,
		&sample.DiskReadBytesPerSec, &sample.DiskWriteBytesPerSec,
		&sample.UptimeSeconds,
	)
	if err != nil {
		return monitoring.MetricSample{}, err
	}
	t, err := parseTime(collectedAt)
	if err != nil {
		return monitoring.MetricSample{}, fmt.Errorf("parse collected_at: %w", err)
	}
	sample.ID = id
	sample.ServerID = server.ServerID(serverID)
	sample.CollectedAt = t
	return sample, nil
}
