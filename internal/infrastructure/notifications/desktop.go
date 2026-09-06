package notifications

import (
	"context"
	"sync"

	"fyne.io/fyne/v2"

	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/logging"
)

// DesktopService sends desktop notifications via Fyne when an app is running,
// and always logs them for observability.
type DesktopService struct {
	log *logging.Logger
	mu  sync.Mutex
	app fyne.App
}

// NewDesktopService constructs a DesktopService.
func NewDesktopService(log *logging.Logger) *DesktopService {
	if log == nil {
		log = logging.New("info")
	}
	return &DesktopService{log: log.WithCategory(logging.CategoryNotify)}
}

// SetApp binds a Fyne application for native notifications.
func (s *DesktopService) SetApp(a fyne.App) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.app = a
}

// Notify records and optionally displays a desktop notification.
func (s *DesktopService) Notify(ctx context.Context, title, body string) error {
	s.log.InfoContext(ctx, "desktop notification", "title", title, "body", body)
	s.mu.Lock()
	a := s.app
	s.mu.Unlock()
	if a == nil {
		a = fyne.CurrentApp()
	}
	if a != nil {
		a.SendNotification(&fyne.Notification{Title: title, Content: body})
	}
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
