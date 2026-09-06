// Package sqlite implements HostDeck persistence with modernc.org/sqlite.
package sqlite

import (
	"context"
	"database/sql"
	"fmt"
	"strings"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/persistence/migrate"

	_ "modernc.org/sqlite"
)

// DB wraps a SQLite connection pool used by repositories.
type DB struct {
	sql *sql.DB
}

// Open opens (or creates) a SQLite database at path.
// Pass ":memory:" for an in-memory database.
func Open(path string) (*DB, error) {
	dsn := path
	if path != ":memory:" && !strings.Contains(path, "?") {
		// Busy timeout helps concurrent readers/writers with WAL.
		dsn = path + "?_pragma=busy_timeout(5000)"
	}

	sqlDB, err := sql.Open("sqlite", dsn)
	if err != nil {
		return nil, fmt.Errorf("open sqlite: %w", err)
	}

	sqlDB.SetMaxOpenConns(1)
	sqlDB.SetConnMaxLifetime(0)

	db := &DB{sql: sqlDB}
	if err := db.configure(); err != nil {
		_ = sqlDB.Close()
		return nil, err
	}
	return db, nil
}

func (db *DB) configure() error {
	if _, err := db.sql.Exec(`PRAGMA foreign_keys = ON`); err != nil {
		return fmt.Errorf("enable foreign_keys: %w", err)
	}

	// WAL is preferred for file DBs; :memory: may ignore or reject it.
	var mode string
	if err := db.sql.QueryRow(`PRAGMA journal_mode = WAL`).Scan(&mode); err != nil {
		// Best-effort: continue without WAL.
		_ = err
	}
	return nil
}

// Migrate applies pending schema migrations.
func (db *DB) Migrate(ctx context.Context) error {
	return migrate.Up(ctx, db.sql)
}

// Close closes the underlying database.
func (db *DB) Close() error {
	if db == nil || db.sql == nil {
		return nil
	}
	return db.sql.Close()
}

// SQL exposes the underlying *sql.DB for advanced use (tests, migrations).
func (db *DB) SQL() *sql.DB {
	return db.sql
}

func boolToInt(v bool) int {
	if v {
		return 1
	}
	return 0
}

func intToBool(v int) bool {
	return v != 0
}

func formatTime(t time.Time) string {
	return t.UTC().Format(time.RFC3339Nano)
}

func parseTime(s string) (time.Time, error) {
	if s == "" {
		return time.Time{}, nil
	}
	if t, err := time.Parse(time.RFC3339Nano, s); err == nil {
		return t.UTC(), nil
	}
	t, err := time.Parse(time.RFC3339, s)
	if err != nil {
		return time.Time{}, err
	}
	return t.UTC(), nil
}

func nullTime(t *time.Time) sql.NullString {
	if t == nil || t.IsZero() {
		return sql.NullString{}
	}
	return sql.NullString{String: formatTime(*t), Valid: true}
}

func parseNullTime(ns sql.NullString) (*time.Time, error) {
	if !ns.Valid || ns.String == "" {
		return nil, nil
	}
	t, err := parseTime(ns.String)
	if err != nil {
		return nil, err
	}
	return &t, nil
}
