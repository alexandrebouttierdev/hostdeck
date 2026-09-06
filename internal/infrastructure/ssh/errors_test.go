package ssh_test

import (
	"context"
	"errors"
	"testing"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
	sshinfra "github.com/alexandrebouttierdev/hostdeck/internal/infrastructure/ssh"
)

func TestTypedSSHErrors(t *testing.T) {
	err := &sshinfra.Error{Op: "dial", Host: "10.0.0.1:22", Err: sshinfra.ErrTimeout}
	if !errors.Is(err, sshinfra.ErrTimeout) {
		t.Fatal("expected unwrap to ErrTimeout")
	}
	if err.Error() == "" {
		t.Fatal("empty error string")
	}
}

type memHostKeys struct {
	data map[string]string
}

func (m *memHostKeys) Lookup(ctx context.Context, host string, port int, keyType string) (string, string, error) {
	_ = ctx
	key := host + ":" + keyType
	fp, ok := m.data[key]
	if !ok {
		return "", "", shared.ErrNotFound
	}
	return fp, "pub", nil
}

func (m *memHostKeys) Store(ctx context.Context, host string, port int, keyType, fingerprint, publicKey string) error {
	_ = ctx
	_ = port
	_ = publicKey
	if m.data == nil {
		m.data = map[string]string{}
	}
	m.data[host+":"+keyType] = fingerprint
	return nil
}

func TestHostKeyStoreAdapter(t *testing.T) {
	store := &memHostKeys{}
	adapter := sshinfra.HostKeyStoreAdapter{
		GetFn:   store.Lookup,
		StoreFn: store.Store,
	}
	ctx := context.Background()
	_, _, err := adapter.Lookup(ctx, "h", 22, "ssh-ed25519")
	if !errors.Is(err, shared.ErrNotFound) {
		t.Fatalf("got %v", err)
	}
	if err := adapter.Store(ctx, "h", 22, "ssh-ed25519", "SHA256:abc", "pub"); err != nil {
		t.Fatal(err)
	}
	fp, _, err := adapter.Lookup(ctx, "h", 22, "ssh-ed25519")
	if err != nil || fp != "SHA256:abc" {
		t.Fatalf("fp=%q err=%v", fp, err)
	}
}
