// Package notifications delivers desktop notifications.
package notifications

import (
	"context"

	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/logging"
)

// DesktopService is a log-based stub for DesktopNotificationService.
// Fyne notification integration can replace the sink later.
type DesktopService struct {
	log *logging.Logger
}

// NewDesktopService constructs a DesktopService.
func NewDesktopService(log *logging.Logger) *DesktopService {
	if log == nil {
		log = logging.New("info")
	}
	return &DesktopService{log: log.WithCategory(logging.CategoryNotify)}
}

// Notify records a desktop notification (stub: logs only).
func (s *DesktopService) Notify(ctx context.Context, title, body string) error {
	s.log.InfoContext(ctx, "desktop notification", "title", title, "body", body)
	return nil
}

// NoopService discards notifications.
type NoopService struct{}

func (NoopService) Notify(ctx context.Context, title, body string) error {
	_ = ctx
	_ = title
	_ = body
	return nil
}
