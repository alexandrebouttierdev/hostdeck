package sqlite

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// HostKeyRecord is a TOFU-stored SSH host key.
type HostKeyRecord struct {
	Host        string
	Port        int
	KeyType     string
	Fingerprint string
	PublicKey   string
	FirstSeenAt time.Time
	LastSeenAt  time.Time
}

// HostKeyStore persists SSH host key fingerprints (TOFU).
type HostKeyStore struct {
	db *DB
}

// NewHostKeyStore constructs a HostKeyStore.
func NewHostKeyStore(db *DB) *HostKeyStore {
	return &HostKeyStore{db: db}
}

// Get returns a stored host key for host:port and key type.
func (s *HostKeyStore) Get(ctx context.Context, host string, port int, keyType string) (HostKeyRecord, error) {
	row := s.db.sql.QueryRowContext(ctx, `
SELECT host, port, key_type, fingerprint, public_key, first_seen_at, last_seen_at
FROM ssh_host_keys WHERE host = ? AND port = ? AND key_type = ?`, host, port, keyType)

	var rec HostKeyRecord
	var first, last string
	err := row.Scan(&rec.Host, &rec.Port, &rec.KeyType, &rec.Fingerprint, &rec.PublicKey, &first, &last)
	if errors.Is(err, sql.ErrNoRows) {
		return HostKeyRecord{}, shared.ErrNotFound
	}
	if err != nil {
		return HostKeyRecord{}, fmt.Errorf("get host key: %w", err)
	}
	rec.FirstSeenAt, err = parseTime(first)
	if err != nil {
		return HostKeyRecord{}, err
	}
	rec.LastSeenAt, err = parseTime(last)
	if err != nil {
		return HostKeyRecord{}, err
	}
	return rec, nil
}

// Upsert stores or refreshes a host key fingerprint.
func (s *HostKeyStore) Upsert(ctx context.Context, rec HostKeyRecord) error {
	now := formatTime(time.Now().UTC())
	first := now
	if !rec.FirstSeenAt.IsZero() {
		first = formatTime(rec.FirstSeenAt)
	}
	last := now
	if !rec.LastSeenAt.IsZero() {
		last = formatTime(rec.LastSeenAt)
	}
	_, err := s.db.sql.ExecContext(ctx, `
INSERT INTO ssh_host_keys (host, port, key_type, fingerprint, public_key, first_seen_at, last_seen_at)
VALUES (?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(host, port, key_type) DO UPDATE SET
	fingerprint = excluded.fingerprint,
	public_key = excluded.public_key,
	last_seen_at = excluded.last_seen_at`,
		rec.Host, rec.Port, rec.KeyType, rec.Fingerprint, rec.PublicKey, first, last,
	)
	if err != nil {
		return fmt.Errorf("upsert host key: %w", err)
	}
	return nil
}
