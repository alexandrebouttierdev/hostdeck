package incident

import (
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// Severity représente la sévérité d'un incident.
type Severity string

const (
	SeverityInformation Severity = "information"
	SeverityWarning     Severity = "warning"
	SeverityAverage     Severity = "average"
	SeverityHigh        Severity = "high"
	SeverityCritical    Severity = "critical"
)

func (s Severity) LabelFR() string {
	switch s {
	case SeverityInformation:
		return "Information"
	case SeverityWarning:
		return "Avertissement"
	case SeverityAverage:
		return "Mineure"
	case SeverityHigh:
		return "Majeure"
	case SeverityCritical:
		return "Critique"
	default:
		return string(s)
	}
}

// IncidentStatus représente l'état d'un incident.
type IncidentStatus string

const (
	IncidentStatusOpen         IncidentStatus = "open"
	IncidentStatusAcknowledged IncidentStatus = "acknowledged"
	IncidentStatusRecovered    IncidentStatus = "recovered"
	IncidentStatusResolved     IncidentStatus = "resolved"
)

func (s IncidentStatus) LabelFR() string {
	switch s {
	case IncidentStatusOpen:
		return "Non acquitté"
	case IncidentStatusAcknowledged:
		return "Acquitté"
	case IncidentStatusRecovered:
		return "Rétabli"
	case IncidentStatusResolved:
		return "Résolu"
	default:
		return string(s)
	}
}

// IncidentID identifie un incident.
type IncidentID string

func NewIncidentID() IncidentID {
	return IncidentID(shared.NewID())
}

func (id IncidentID) String() string { return string(id) }

// Incident représente un incident actif ou historique.
type Incident struct {
	ID              IncidentID
	DisplayID       string
	ServerID        server.ServerID
	ServerName      string
	Metric          string
	Problem         string
	Severity        Severity
	RuleID          string
	RuleName        string
	Status          IncidentStatus
	CurrentValue    float64
	Threshold       float64
	StartedAt       time.Time
	LastUpdatedAt   time.Time
	AcknowledgedAt  *time.Time
	AcknowledgedBy  string
	RecoveredAt     *time.Time
	ResolvedAt      *time.Time
	Notes           string
}

func (i Incident) Duration() time.Duration {
	end := time.Now()
	if i.ResolvedAt != nil {
		end = *i.ResolvedAt
	} else if i.RecoveredAt != nil {
		end = *i.RecoveredAt
	}
	return end.Sub(i.StartedAt)
}

// AlertRule définit une règle d'évaluation d'alerte.
type AlertRule struct {
	ID               string
	Name             string
	Enabled          bool
	Metric           string
	Operator         string
	Threshold        float64
	DurationSeconds  int
	Severity         Severity
	CooldownSeconds  int
	Scope            string
	Category         string
	Expression       string
	LastTriggeredAt  *time.Time
	Status           string
	CreatedAt        time.Time
	UpdatedAt        time.Time
}

func (r *AlertRule) Validate() error {
	if r.Name == "" {
		return shared.NewValidationError("name", "name is required")
	}
	if r.Metric == "" {
		return shared.NewValidationError("metric", "metric is required")
	}
	if r.DurationSeconds <= 0 {
		r.DurationSeconds = 300
	}
	if r.CooldownSeconds < 0 {
		r.CooldownSeconds = 0
	}
	return nil
}
