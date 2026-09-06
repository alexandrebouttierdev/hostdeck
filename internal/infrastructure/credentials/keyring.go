// Package credentials stores secrets outside SQLite.
package credentials

import (
	"context"

	"github.com/zalando/go-keyring"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

const keyringService = "hostdeck"

// KeyringStore persists secrets in the OS keyring (service "hostdeck").
type KeyringStore struct{}

// NewKeyringStore constructs a KeyringStore.
func NewKeyringStore() *KeyringStore { return &KeyringStore{} }

// Set stores a secret under ref (username in keyring terms).
func (s *KeyringStore) Set(ctx context.Context, ref string, secret []byte) error {
	_ = ctx
	return keyring.Set(keyringService, ref, string(secret))
}

// Get retrieves a secret.
func (s *KeyringStore) Get(ctx context.Context, ref string) ([]byte, error) {
	_ = ctx
	val, err := keyring.Get(keyringService, ref)
	if err != nil {
		if err == keyring.ErrNotFound {
			return nil, shared.ErrNotFound
		}
		return nil, err
	}
	return []byte(val), nil
}

// Delete removes a secret.
func (s *KeyringStore) Delete(ctx context.Context, ref string) error {
	_ = ctx
	err := keyring.Delete(keyringService, ref)
	if err == keyring.ErrNotFound {
		return shared.ErrNotFound
	}
	return err
}
