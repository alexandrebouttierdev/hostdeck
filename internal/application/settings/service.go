// Package settings implements settings get/update use cases.
package settings

import (
	"context"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
)

// Service groups settings use cases.
type Service struct {
	repo  ports.SettingsRepository
	clock ports.Clock
}

// NewService constructs a settings Service.
func NewService(repo ports.SettingsRepository, clock ports.Clock) *Service {
	if clock == nil {
		clock = ports.SystemClock()
	}
	return &Service{repo: repo, clock: clock}
}

// Get returns current settings (defaults if none stored).
func (s *Service) Get(ctx context.Context) (dto.SettingsDTO, error) {
	cfg, err := s.repo.Get(ctx)
	if err != nil {
		return dto.SettingsDTO{}, err
	}
	return dto.FromSettings(cfg), nil
}

// Update persists settings.
func (s *Service) Update(ctx context.Context, in dto.SettingsDTO) (dto.SettingsDTO, error) {
	cfg := dto.ToSettings(in)
	cfg.UpdatedAt = s.clock.Now()
	if cfg.DefaultIntervalSeconds <= 0 {
		cfg.DefaultIntervalSeconds = 60
	}
	if cfg.RequestTimeoutSeconds <= 0 {
		cfg.RequestTimeoutSeconds = 10
	}
	if cfg.Language == "" {
		cfg.Language = "fr"
	}
	if err := s.repo.Save(ctx, cfg); err != nil {
		return dto.SettingsDTO{}, err
	}
	return dto.FromSettings(cfg), nil
}
