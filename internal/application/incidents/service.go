// Package incidents implements incident lifecycle use cases.
package incidents

import (
	"context"
	"strings"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/incident"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// Service groups incident use cases.
type Service struct {
	repo  ports.IncidentRepository
	clock ports.Clock
}

// NewService constructs an incidents Service.
func NewService(repo ports.IncidentRepository, clock ports.Clock) *Service {
	if clock == nil {
		clock = ports.SystemClock()
	}
	return &Service{repo: repo, clock: clock}
}

// List returns incidents (active only when onlyActive is true).
func (s *Service) List(ctx context.Context, onlyActive bool) ([]dto.IncidentDTO, error) {
	list, err := s.repo.List(ctx, onlyActive)
	if err != nil {
		return nil, err
	}
	out := make([]dto.IncidentDTO, 0, len(list))
	for _, inc := range list {
		out = append(out, dto.FromIncident(inc))
	}
	return out, nil
}

// GetDetails returns one incident.
func (s *Service) GetDetails(ctx context.Context, id string) (dto.IncidentDetailsDTO, error) {
	incID := incident.IncidentID(id)
	if !shared.IsValidID(id) {
		return dto.IncidentDetailsDTO{}, shared.NewValidationError("id", "invalid incident id")
	}
	inc, err := s.repo.Get(ctx, incID)
	if err != nil {
		return dto.IncidentDetailsDTO{}, err
	}
	return dto.FromIncidentDetails(inc), nil
}

// Acknowledge marks an incident as acknowledged.
func (s *Service) Acknowledge(ctx context.Context, in dto.AcknowledgeIncidentDTO) (dto.IncidentDetailsDTO, error) {
	if !shared.IsValidID(in.IncidentID) {
		return dto.IncidentDetailsDTO{}, shared.NewValidationError("incident_id", "invalid incident id")
	}
	by := strings.TrimSpace(in.By)
	if by == "" {
		return dto.IncidentDetailsDTO{}, shared.NewValidationError("by", "acknowledged by is required")
	}

	inc, err := s.repo.Get(ctx, incident.IncidentID(in.IncidentID))
	if err != nil {
		return dto.IncidentDetailsDTO{}, err
	}
	switch inc.Status {
	case incident.IncidentStatusResolved, incident.IncidentStatusRecovered:
		return dto.IncidentDetailsDTO{}, shared.NewValidationError("status", "cannot acknowledge a closed incident")
	}

	now := s.clock.Now()
	inc.Status = incident.IncidentStatusAcknowledged
	inc.AcknowledgedAt = &now
	inc.AcknowledgedBy = by
	inc.LastUpdatedAt = now
	if note := strings.TrimSpace(in.Notes); note != "" {
		if inc.Notes != "" {
			inc.Notes = inc.Notes + "\n" + note
		} else {
			inc.Notes = note
		}
	}
	if err := s.repo.Update(ctx, inc); err != nil {
		return dto.IncidentDetailsDTO{}, err
	}
	return dto.FromIncidentDetails(inc), nil
}

// Resolve marks an incident as resolved.
func (s *Service) Resolve(ctx context.Context, in dto.ResolveIncidentDTO) (dto.IncidentDetailsDTO, error) {
	if !shared.IsValidID(in.IncidentID) {
		return dto.IncidentDetailsDTO{}, shared.NewValidationError("incident_id", "invalid incident id")
	}
	inc, err := s.repo.Get(ctx, incident.IncidentID(in.IncidentID))
	if err != nil {
		return dto.IncidentDetailsDTO{}, err
	}
	if inc.Status == incident.IncidentStatusResolved {
		return dto.FromIncidentDetails(inc), nil
	}

	now := s.clock.Now()
	inc.Status = incident.IncidentStatusResolved
	inc.ResolvedAt = &now
	inc.LastUpdatedAt = now
	if note := strings.TrimSpace(in.Notes); note != "" {
		if inc.Notes != "" {
			inc.Notes = inc.Notes + "\n" + note
		} else {
			inc.Notes = note
		}
	}
	if err := s.repo.Update(ctx, inc); err != nil {
		return dto.IncidentDetailsDTO{}, err
	}
	return dto.FromIncidentDetails(inc), nil
}
