// Package alerts implements alert rule CRUD use cases.
package alerts

import (
	"context"
	"strings"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// Service groups alert rule use cases.
type Service struct {
	repo  ports.AlertRuleRepository
	clock ports.Clock
}

// NewService constructs an alerts Service.
func NewService(repo ports.AlertRuleRepository, clock ports.Clock) *Service {
	if clock == nil {
		clock = ports.SystemClock()
	}
	return &Service{repo: repo, clock: clock}
}

// List returns all alert rules.
func (s *Service) List(ctx context.Context) ([]dto.AlertRuleDTO, error) {
	rules, err := s.repo.List(ctx)
	if err != nil {
		return nil, err
	}
	out := make([]dto.AlertRuleDTO, 0, len(rules))
	for _, r := range rules {
		out = append(out, dto.FromAlertRule(r))
	}
	return out, nil
}

// Get returns one alert rule.
func (s *Service) Get(ctx context.Context, id string) (dto.AlertRuleDTO, error) {
	if strings.TrimSpace(id) == "" {
		return dto.AlertRuleDTO{}, shared.NewValidationError("id", "id is required")
	}
	rule, err := s.repo.Get(ctx, id)
	if err != nil {
		return dto.AlertRuleDTO{}, err
	}
	return dto.FromAlertRule(rule), nil
}

// Create creates a new alert rule.
func (s *Service) Create(ctx context.Context, in dto.AlertRuleDTO) (dto.AlertRuleDTO, error) {
	rule := dto.ToAlertRule(in)
	now := s.clock.Now()
	rule.CreatedAt = now
	rule.UpdatedAt = now
	if rule.Status == "" {
		rule.Status = "ok"
	}
	if err := rule.Validate(); err != nil {
		return dto.AlertRuleDTO{}, err
	}
	if err := s.repo.Create(ctx, rule); err != nil {
		return dto.AlertRuleDTO{}, err
	}
	return dto.FromAlertRule(rule), nil
}

// Update updates an existing alert rule.
func (s *Service) Update(ctx context.Context, in dto.AlertRuleDTO) (dto.AlertRuleDTO, error) {
	if strings.TrimSpace(in.ID) == "" {
		return dto.AlertRuleDTO{}, shared.NewValidationError("id", "id is required")
	}
	existing, err := s.repo.Get(ctx, in.ID)
	if err != nil {
		return dto.AlertRuleDTO{}, err
	}
	rule := dto.ToAlertRule(in)
	rule.CreatedAt = existing.CreatedAt
	rule.UpdatedAt = s.clock.Now()
	if err := rule.Validate(); err != nil {
		return dto.AlertRuleDTO{}, err
	}
	if err := s.repo.Update(ctx, rule); err != nil {
		return dto.AlertRuleDTO{}, err
	}
	return dto.FromAlertRule(rule), nil
}

// Delete removes an alert rule.
func (s *Service) Delete(ctx context.Context, id string) error {
	if strings.TrimSpace(id) == "" {
		return shared.NewValidationError("id", "id is required")
	}
	return s.repo.Delete(ctx, id)
}
