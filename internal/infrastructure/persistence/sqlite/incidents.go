package sqlite

import (
	"context"
	"database/sql"
	"errors"
	"fmt"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// IncidentRepository persists incidents in SQLite.
type IncidentRepository struct {
	db *DB
}

// NewIncidentRepository constructs an IncidentRepository.
func NewIncidentRepository(db *DB) *IncidentRepository {
	return &IncidentRepository{db: db}
}

const incidentColumns = `
	id, display_id, server_id, server_name, metric, problem, severity, rule_id, rule_name,
	status, current_value, threshold, started_at, last_updated_at,
	acknowledged_at, acknowledged_by, recovered_at, resolved_at, notes`

func (r *IncidentRepository) List(ctx context.Context, onlyActive bool) ([]incident.Incident, error) {
	query := `SELECT ` + incidentColumns + ` FROM incidents`
	if onlyActive {
		query += ` WHERE status IN ('open', 'acknowledged')`
	}
	query += ` ORDER BY started_at DESC`

	rows, err := r.db.sql.QueryContext(ctx, query)
	if err != nil {
		return nil, fmt.Errorf("list incidents: %w", err)
	}
	defer rows.Close()

	var out []incident.Incident
	for rows.Next() {
		inc, err := scanIncident(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, inc)
	}
	return out, rows.Err()
}

func (r *IncidentRepository) Get(ctx context.Context, id incident.IncidentID) (incident.Incident, error) {
	row := r.db.sql.QueryRowContext(ctx,
		`SELECT `+incidentColumns+` FROM incidents WHERE id = ?`, id.String())
	inc, err := scanIncident(row)
	if errors.Is(err, sql.ErrNoRows) {
		return incident.Incident{}, shared.ErrNotFound
	}
	if err != nil {
		return incident.Incident{}, fmt.Errorf("get incident: %w", err)
	}
	return inc, nil
}

func (r *IncidentRepository) Create(ctx context.Context, inc incident.Incident) error {
	_, err := r.db.sql.ExecContext(ctx, `
INSERT INTO incidents (`+incidentColumns+`)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		inc.ID.String(),
		inc.DisplayID,
		inc.ServerID.String(),
		inc.ServerName,
		inc.Metric,
		inc.Problem,
		string(inc.Severity),
		inc.RuleID,
		inc.RuleName,
		string(inc.Status),
		inc.CurrentValue,
		inc.Threshold,
		formatTime(inc.StartedAt),
		formatTime(inc.LastUpdatedAt),
		nullTime(inc.AcknowledgedAt),
		inc.AcknowledgedBy,
		nullTime(inc.RecoveredAt),
		nullTime(inc.ResolvedAt),
		inc.Notes,
	)
	if err != nil {
		return fmt.Errorf("create incident: %w", err)
	}
	return nil
}

func (r *IncidentRepository) Update(ctx context.Context, inc incident.Incident) error {
	res, err := r.db.sql.ExecContext(ctx, `
UPDATE incidents SET
	display_id = ?, server_id = ?, server_name = ?, metric = ?, problem = ?,
	severity = ?, rule_id = ?, rule_name = ?, status = ?, current_value = ?, threshold = ?,
	started_at = ?, last_updated_at = ?, acknowledged_at = ?, acknowledged_by = ?,
	recovered_at = ?, resolved_at = ?, notes = ?
WHERE id = ?`,
		inc.DisplayID,
		inc.ServerID.String(),
		inc.ServerName,
		inc.Metric,
		inc.Problem,
		string(inc.Severity),
		inc.RuleID,
		inc.RuleName,
		string(inc.Status),
		inc.CurrentValue,
		inc.Threshold,
		formatTime(inc.StartedAt),
		formatTime(inc.LastUpdatedAt),
		nullTime(inc.AcknowledgedAt),
		inc.AcknowledgedBy,
		nullTime(inc.RecoveredAt),
		nullTime(inc.ResolvedAt),
		inc.Notes,
		inc.ID.String(),
	)
	if err != nil {
		return fmt.Errorf("update incident: %w", err)
	}
	n, err := res.RowsAffected()
	if err != nil {
		return err
	}
	if n == 0 {
		return shared.ErrNotFound
	}
	return nil
}

func scanIncident(row scannable) (incident.Incident, error) {
	var (
		id, displayID, serverID, serverName, metric, problem string
		severity, ruleID, ruleName, status                   string
		currentValue, threshold                             float64
		startedAt, lastUpdatedAt                            string
		acknowledgedAt, recoveredAt, resolvedAt             sql.NullString
		acknowledgedBy, notes                               string
	)
	err := row.Scan(
		&id, &displayID, &serverID, &serverName, &metric, &problem,
		&severity, &ruleID, &ruleName, &status, &currentValue, &threshold,
		&startedAt, &lastUpdatedAt, &acknowledgedAt, &acknowledgedBy,
		&recoveredAt, &resolvedAt, &notes,
	)
	if err != nil {
		return incident.Incident{}, err
	}

	started, err := parseTime(startedAt)
	if err != nil {
		return incident.Incident{}, err
	}
	updated, err := parseTime(lastUpdatedAt)
	if err != nil {
		return incident.Incident{}, err
	}
	ackAt, err := parseNullTime(acknowledgedAt)
	if err != nil {
		return incident.Incident{}, err
	}
	recAt, err := parseNullTime(recoveredAt)
	if err != nil {
		return incident.Incident{}, err
	}
	resAt, err := parseNullTime(resolvedAt)
	if err != nil {
		return incident.Incident{}, err
	}

	return incident.Incident{
		ID:             incident.IncidentID(id),
		DisplayID:      displayID,
		ServerID:       server.ServerID(serverID),
		ServerName:     serverName,
		Metric:         metric,
		Problem:        problem,
		Severity:       incident.Severity(severity),
		RuleID:         ruleID,
		RuleName:       ruleName,
		Status:         incident.IncidentStatus(status),
		CurrentValue:   currentValue,
		Threshold:      threshold,
		StartedAt:      started,
		LastUpdatedAt:  updated,
		AcknowledgedAt: ackAt,
		AcknowledgedBy: acknowledgedBy,
		RecoveredAt:    recAt,
		ResolvedAt:     resAt,
		Notes:          notes,
	}, nil
}
