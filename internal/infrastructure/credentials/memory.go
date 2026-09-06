package credentials

import (
	"context"
	"sync"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// MemoryStore is an in-memory CredentialStore for tests and headless environments.
type MemoryStore struct {
	mu   sync.RWMutex
	data map[string][]byte
}

// NewMemoryStore constructs an empty MemoryStore.
func NewMemoryStore() *MemoryStore {
	return &MemoryStore{data: make(map[string][]byte)}
}

// Set stores a secret.
func (s *MemoryStore) Set(ctx context.Context, ref string, secret []byte) error {
	_ = ctx
	s.mu.Lock()
	defer s.mu.Unlock()
	cp := make([]byte, len(secret))
	copy(cp, secret)
	s.data[ref] = cp
	return nil
}

// Get retrieves a secret.
func (s *MemoryStore) Get(ctx context.Context, ref string) ([]byte, error) {
	_ = ctx
	s.mu.RLock()
	defer s.mu.RUnlock()
	val, ok := s.data[ref]
	if !ok {
		return nil, shared.ErrNotFound
	}
	cp := make([]byte, len(val))
	copy(cp, val)
	return cp, nil
}

// Delete removes a secret.
func (s *MemoryStore) Delete(ctx context.Context, ref string) error {
	_ = ctx
	s.mu.Lock()
	defer s.mu.Unlock()
	if _, ok := s.data[ref]; !ok {
		return shared.ErrNotFound
	}
	delete(s.data, ref)
	return nil
}
