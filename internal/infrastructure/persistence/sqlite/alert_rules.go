package sqlite

import (
	"context"
	"database/sql"
	"errors"
	"fmt"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// AlertRuleRepository persists alert rules in SQLite.
type AlertRuleRepository struct {
	db *DB
}

// NewAlertRuleRepository constructs an AlertRuleRepository.
func NewAlertRuleRepository(db *DB) *AlertRuleRepository {
	return &AlertRuleRepository{db: db}
}

const alertRuleColumns = `
	id, name, enabled, metric, operator, threshold, duration_seconds, severity,
	cooldown_seconds, scope, category, expression, last_triggered_at, status, created_at, updated_at`

func (r *AlertRuleRepository) List(ctx context.Context) ([]incident.AlertRule, error) {
	rows, err := r.db.sql.QueryContext(ctx,
		`SELECT `+alertRuleColumns+` FROM alert_rules ORDER BY name COLLATE NOCASE, id`)
	if err != nil {
		return nil, fmt.Errorf("list alert rules: %w", err)
	}
	defer rows.Close()

	var out []incident.AlertRule
	for rows.Next() {
		rule, err := scanAlertRule(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, rule)
	}
	return out, rows.Err()
}

func (r *AlertRuleRepository) Get(ctx context.Context, id string) (incident.AlertRule, error) {
	row := r.db.sql.QueryRowContext(ctx,
		`SELECT `+alertRuleColumns+` FROM alert_rules WHERE id = ?`, id)
	rule, err := scanAlertRule(row)
	if errors.Is(err, sql.ErrNoRows) {
		return incident.AlertRule{}, shared.ErrNotFound
	}
	if err != nil {
		return incident.AlertRule{}, fmt.Errorf("get alert rule: %w", err)
	}
	return rule, nil
}

func (r *AlertRuleRepository) Create(ctx context.Context, rule incident.AlertRule) error {
	if err := rule.Validate(); err != nil {
		return err
	}
	_, err := r.db.sql.ExecContext(ctx, `
INSERT INTO alert_rules (`+alertRuleColumns+`)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		rule.ID,
		rule.Name,
		boolToInt(rule.Enabled),
		rule.Metric,
		rule.Operator,
		rule.Threshold,
		rule.DurationSeconds,
		string(rule.Severity),
		rule.CooldownSeconds,
		rule.Scope,
		rule.Category,
		rule.Expression,
		nullTime(rule.LastTriggeredAt),
		rule.Status,
		formatTime(rule.CreatedAt),
		formatTime(rule.UpdatedAt),
	)
	if err != nil {
		return fmt.Errorf("create alert rule: %w", err)
	}
	return nil
}

func (r *AlertRuleRepository) Update(ctx context.Context, rule incident.AlertRule) error {
	if err := rule.Validate(); err != nil {
		return err
	}
	res, err := r.db.sql.ExecContext(ctx, `
UPDATE alert_rules SET
	name = ?, enabled = ?, metric = ?, operator = ?, threshold = ?, duration_seconds = ?,
	severity = ?, cooldown_seconds = ?, scope = ?, category = ?, expression = ?,
	last_triggered_at = ?, status = ?, updated_at = ?
WHERE id = ?`,
		rule.Name,
		boolToInt(rule.Enabled),
		rule.Metric,
		rule.Operator,
		rule.Threshold,
		rule.DurationSeconds,
		string(rule.Severity),
		rule.CooldownSeconds,
		rule.Scope,
		rule.Category,
		rule.Expression,
		nullTime(rule.LastTriggeredAt),
		rule.Status,
		formatTime(rule.UpdatedAt),
		rule.ID,
	)
	if err != nil {
		return fmt.Errorf("update alert rule: %w", err)
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

func (r *AlertRuleRepository) Delete(ctx context.Context, id string) error {
	res, err := r.db.sql.ExecContext(ctx, `DELETE FROM alert_rules WHERE id = ?`, id)
	if err != nil {
		return fmt.Errorf("delete alert rule: %w", err)
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

func scanAlertRule(row scannable) (incident.AlertRule, error) {
	var (
		id, name, metric, operator, severity string
		enabled                              int
		threshold                            float64
		durationSeconds, cooldownSeconds     int
		scope, category, expression, status  string
		lastTriggeredAt                      sql.NullString
		createdAt, updatedAt                 string
	)
	err := row.Scan(
		&id, &name, &enabled, &metric, &operator, &threshold, &durationSeconds, &severity,
		&cooldownSeconds, &scope, &category, &expression, &lastTriggeredAt, &status,
		&createdAt, &updatedAt,
	)
	if err != nil {
		return incident.AlertRule{}, err
	}

	created, err := parseTime(createdAt)
	if err != nil {
		return incident.AlertRule{}, err
	}
	updated, err := parseTime(updatedAt)
	if err != nil {
		return incident.AlertRule{}, err
	}
	lastTrig, err := parseNullTime(lastTriggeredAt)
	if err != nil {
		return incident.AlertRule{}, err
	}

	return incident.AlertRule{
		ID:              id,
		Name:            name,
		Enabled:         intToBool(enabled),
		Metric:          metric,
		Operator:        operator,
		Threshold:       threshold,
		DurationSeconds: durationSeconds,
		Severity:        incident.Severity(severity),
		CooldownSeconds: cooldownSeconds,
		Scope:           scope,
		Category:        category,
		Expression:      expression,
		LastTriggeredAt: lastTrig,
		Status:          status,
		CreatedAt:       created,
		UpdatedAt:       updated,
	}, nil
}
