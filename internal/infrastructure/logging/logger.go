// Package logging provides a thin structured slog wrapper with categories.
package logging

import (
	"context"
	"log/slog"
	"os"
	"strings"
)

// Category groups log events for filtering.
type Category string

const (
	CategoryApp      Category = "app"
	CategorySSH      Category = "ssh"
	CategoryMetrics  Category = "metrics"
	CategoryDocker   Category = "docker"
	CategoryDB       Category = "db"
	CategoryNotify   Category = "notify"
	CategorySecurity Category = "security"
)

// Logger wraps slog.Logger with a category field.
type Logger struct {
	base *slog.Logger
}

// New creates a JSON logger writing to stderr.
func New(level string) *Logger {
	var lvl slog.Level
	switch strings.ToLower(level) {
	case "debug":
		lvl = slog.LevelDebug
	case "warn", "warning":
		lvl = slog.LevelWarn
	case "error":
		lvl = slog.LevelError
	default:
		lvl = slog.LevelInfo
	}
	handler := slog.NewJSONHandler(os.Stderr, &slog.HandlerOptions{Level: lvl})
	return &Logger{base: slog.New(handler)}
}

// WithCategory returns a child logger tagged with category.
func (l *Logger) WithCategory(cat Category) *Logger {
	return &Logger{base: l.base.With(slog.String("category", string(cat)))}
}

func (l *Logger) Debug(msg string, args ...any) { l.base.Debug(msg, args...) }
func (l *Logger) Info(msg string, args ...any)  { l.base.Info(msg, args...) }
func (l *Logger) Warn(msg string, args ...any)  { l.base.Warn(msg, args...) }
func (l *Logger) Error(msg string, args ...any) { l.base.Error(msg, args...) }

// DebugContext logs at debug with context.
func (l *Logger) DebugContext(ctx context.Context, msg string, args ...any) {
	l.base.DebugContext(ctx, msg, args...)
}

// InfoContext logs at info with context.
func (l *Logger) InfoContext(ctx context.Context, msg string, args ...any) {
	l.base.InfoContext(ctx, msg, args...)
}

// WarnContext logs at warn with context.
func (l *Logger) WarnContext(ctx context.Context, msg string, args ...any) {
	l.base.WarnContext(ctx, msg, args...)
}

// ErrorContext logs at error with context.
func (l *Logger) ErrorContext(ctx context.Context, msg string, args ...any) {
	l.base.ErrorContext(ctx, msg, args...)
}

// Slog exposes the underlying slog.Logger.
func (l *Logger) Slog() *slog.Logger { return l.base }
