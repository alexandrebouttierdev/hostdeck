package ssh

import (
	"bytes"
	"context"
	"encoding/base64"
	"fmt"
	"net"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"golang.org/x/crypto/ssh"
	"golang.org/x/crypto/ssh/agent"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// HostKeyStore abstracts TOFU host key persistence.
type HostKeyStore interface {
	Lookup(ctx context.Context, host string, port int, keyType string) (fingerprint string, publicKey string, err error)
	Store(ctx context.Context, host string, port int, keyType, fingerprint, publicKey string) error
}

// FactoryConfig tunes SSH dial behaviour.
type FactoryConfig struct {
	DialTimeout     time.Duration
	KeepAlivePeriod time.Duration
	KeepAliveCount  int
}

func (c FactoryConfig) withDefaults() FactoryConfig {
	if c.DialTimeout <= 0 {
		c.DialTimeout = 15 * time.Second
	}
	if c.KeepAlivePeriod <= 0 {
		c.KeepAlivePeriod = 30 * time.Second
	}
	if c.KeepAliveCount <= 0 {
		c.KeepAliveCount = 3
	}
	return c
}

// Factory creates SSH connections (direct or via jump host).
type Factory struct {
	servers     ports.ServerRepository
	credentials ports.CredentialStore
	hostKeys    HostKeyStore
	cfg         FactoryConfig
}

// NewFactory constructs an SSHConnectionFactory.
func NewFactory(
	servers ports.ServerRepository,
	credentials ports.CredentialStore,
	hostKeys HostKeyStore,
	cfg FactoryConfig,
) *Factory {
	return &Factory{
		servers:     servers,
		credentials: credentials,
		hostKeys:    hostKeys,
		cfg:         cfg.withDefaults(),
	}
}

// Connect establishes an SSH session to srv (direct or jump).
func (f *Factory) Connect(ctx context.Context, srv server.Server) (ports.SSHConnection, error) {
	switch srv.ConnectionMode {
	case server.ConnectionModeJump:
		return f.connectJump(ctx, srv)
	default:
		return f.connectDirect(ctx, srv)
	}
}

func (f *Factory) connectDirect(ctx context.Context, srv server.Server) (ports.SSHConnection, error) {
	client, err := f.dial(ctx, srv, nil)
	if err != nil {
		return nil, err
	}
	return newConnection(client, f.cfg), nil
}

func (f *Factory) connectJump(ctx context.Context, srv server.Server) (ports.SSHConnection, error) {
	if srv.JumpHostID == nil || *srv.JumpHostID == "" {
		return nil, wrapErr("connect", srv.Host, ErrJumpHostRequired)
	}
	bastion, err := f.servers.Get(ctx, *srv.JumpHostID)
	if err != nil {
		if err == shared.ErrNotFound {
			return nil, wrapErr("connect", srv.Host, ErrJumpHostRequired)
		}
		return nil, wrapErr("connect", srv.Host, fmt.Errorf("%w: %v", ErrJumpHostUnavailable, err))
	}

	bastionClient, err := f.dial(ctx, bastion, nil)
	if err != nil {
		return nil, wrapErr("bastion", bastion.Host, fmt.Errorf("%w: %v", ErrJumpHostUnavailable, err))
	}

	targetAddr := net.JoinHostPort(srv.Host, fmt.Sprintf("%d", srv.Port))
	dialer := func(network, addr string) (net.Conn, error) {
		dctx, cancel := context.WithTimeout(ctx, f.cfg.DialTimeout)
		defer cancel()
		type result struct {
			c   net.Conn
			err error
		}
		ch := make(chan result, 1)
		go func() {
			c, err := bastionClient.Dial(network, addr)
			ch <- result{c, err}
		}()
		select {
		case <-dctx.Done():
			return nil, wrapErr("jump-dial", targetAddr, ErrTimeout)
		case r := <-ch:
			return r.c, r.err
		}
	}

	targetClient, err := f.dial(ctx, srv, dialer)
	if err != nil {
		_ = bastionClient.Close()
		return nil, err
	}
	return newJumpConnection(targetClient, bastionClient, f.cfg), nil
}

type netDialer func(network, addr string) (net.Conn, error)

func (f *Factory) dial(ctx context.Context, srv server.Server, via netDialer) (*ssh.Client, error) {
	addr := net.JoinHostPort(srv.Host, fmt.Sprintf("%d", portOrDefault(srv.Port)))
	authMethods, err := f.authMethods(ctx, srv)
	if err != nil {
		return nil, wrapErr("auth", addr, err)
	}
	if len(authMethods) == 0 {
		return nil, wrapErr("auth", addr, ErrNoAuthMethod)
	}

	config := &ssh.ClientConfig{
		User:            srv.Username,
		Auth:            authMethods,
		HostKeyCallback: f.hostKeyCallback(ctx, srv.Host, portOrDefault(srv.Port)),
		Timeout:         f.cfg.DialTimeout,
	}

	var conn net.Conn
	if via != nil {
		conn, err = via("tcp", addr)
	} else {
		var d net.Dialer
		d.Timeout = f.cfg.DialTimeout
		conn, err = d.DialContext(ctx, "tcp", addr)
	}
	if err != nil {
		if ctx.Err() != nil || os.IsTimeout(err) || strings.Contains(strings.ToLower(err.Error()), "timeout") {
			return nil, wrapErr("dial", addr, fmt.Errorf("%w: %v", ErrTimeout, err))
		}
		return nil, wrapErr("dial", addr, fmt.Errorf("%w: %v", ErrDialFailed, err))
	}

	deadline := time.Now().Add(f.cfg.DialTimeout)
	_ = conn.SetDeadline(deadline)

	c, chans, reqs, err := ssh.NewClientConn(conn, addr, config)
	if err != nil {
		_ = conn.Close()
		msg := strings.ToLower(err.Error())
		if strings.Contains(msg, "unable to authenticate") || strings.Contains(msg, "no supported methods") {
			return nil, wrapErr("handshake", addr, fmt.Errorf("%w: %v", ErrAuthenticationFailed, err))
		}
		if strings.Contains(msg, "host key") {
			return nil, wrapErr("handshake", addr, err)
		}
		return nil, wrapErr("handshake", addr, err)
	}
	_ = conn.SetDeadline(time.Time{})
	return ssh.NewClient(c, chans, reqs), nil
}

func (f *Factory) authMethods(ctx context.Context, srv server.Server) ([]ssh.AuthMethod, error) {
	var methods []ssh.AuthMethod
	var secret []byte
	if f.credentials != nil && srv.CredentialRef != "" {
		var err error
		secret, err = f.credentials.Get(ctx, srv.CredentialRef)
		if err != nil && err != shared.ErrNotFound {
			return nil, err
		}
	}

	switch srv.AuthMethod {
	case server.AuthMethodPassword:
		if len(secret) == 0 {
			return nil, ErrNoAuthMethod
		}
		methods = append(methods, ssh.Password(string(secret)))
	case server.AuthMethodAgent:
		if ag := agentAuth(); ag != nil {
			methods = append(methods, ag)
		}
	case server.AuthMethodKey, "":
		if len(secret) > 0 {
			signer, err := parsePrivateKey(secret)
			if err != nil {
				return nil, fmt.Errorf("%w: %v", ErrAuthenticationFailed, err)
			}
			methods = append(methods, ssh.PublicKeys(signer))
		}
		if ag := agentAuth(); ag != nil {
			methods = append(methods, ag)
		}
		// Fallback: try default identity files when no credential stored.
		if len(methods) == 0 {
			for _, path := range defaultIdentityFiles() {
				data, err := os.ReadFile(path)
				if err != nil {
					continue
				}
				signer, err := parsePrivateKey(data)
				if err != nil {
					continue
				}
				methods = append(methods, ssh.PublicKeys(signer))
				break
			}
		}
	default:
		return nil, ErrNoAuthMethod
	}
	return methods, nil
}

func parsePrivateKey(pemBytes []byte) (ssh.Signer, error) {
	signer, err := ssh.ParsePrivateKey(pemBytes)
	if err == nil {
		return signer, nil
	}
	// Passphrase-protected keys are not supported without a separate passphrase store.
	return nil, err
}

func agentAuth() ssh.AuthMethod {
	sock := os.Getenv("SSH_AUTH_SOCK")
	if sock == "" {
		return nil
	}
	conn, err := net.Dial("unix", sock)
	if err != nil {
		return nil
	}
	return ssh.PublicKeysCallback(agent.NewClient(conn).Signers)
}

func defaultIdentityFiles() []string {
	home, err := os.UserHomeDir()
	if err != nil {
		return nil
	}
	sshDir := filepath.Join(home, ".ssh")
	return []string{
		filepath.Join(sshDir, "id_ed25519"),
		filepath.Join(sshDir, "id_rsa"),
		filepath.Join(sshDir, "id_ecdsa"),
	}
}

func (f *Factory) hostKeyCallback(ctx context.Context, host string, port int) ssh.HostKeyCallback {
	return func(hostname string, remote net.Addr, key ssh.PublicKey) error {
		keyType := key.Type()
		fp := ssh.FingerprintSHA256(key)
		pub := base64.StdEncoding.EncodeToString(key.Marshal())

		if f.hostKeys == nil {
			// Unsafe fallback for tests without a store: accept once (no persistence).
			return nil
		}

		storedFP, _, err := f.hostKeys.Lookup(ctx, host, port, keyType)
		if err == shared.ErrNotFound {
			// TOFU: first connection — trust and store.
			if storeErr := f.hostKeys.Store(ctx, host, port, keyType, fp, pub); storeErr != nil {
				return wrapErr("hostkey", host, storeErr)
			}
			return nil
		}
		if err != nil {
			return wrapErr("hostkey", host, err)
		}
		if storedFP != fp {
			return wrapErr("hostkey", host, fmt.Errorf("%w: expected %s got %s", ErrHostKeyMismatch, storedFP, fp))
		}
		// Refresh last-seen.
		_ = f.hostKeys.Store(ctx, host, port, keyType, fp, pub)
		return nil
	}
}

func portOrDefault(port int) int {
	if port <= 0 {
		return 22
	}
	return port
}

// connection wraps *ssh.Client as ports.SSHConnection.
type connection struct {
	client *ssh.Client
	cfg    FactoryConfig
	mu     sync.Mutex
	closed bool
}

func newConnection(client *ssh.Client, cfg FactoryConfig) *connection {
	c := &connection{client: client, cfg: cfg}
	go c.keepAlive()
	return c
}

func (c *connection) Execute(ctx context.Context, command string) (ports.CommandResult, error) {
	c.mu.Lock()
	closed := c.closed
	client := c.client
	c.mu.Unlock()
	if closed || client == nil {
		return ports.CommandResult{}, ErrClosed
	}

	session, err := client.NewSession()
	if err != nil {
		return ports.CommandResult{}, wrapErr("session", "", err)
	}
	defer session.Close()

	var stdout, stderr bytes.Buffer
	session.Stdout = &stdout
	session.Stderr = &stderr

	start := time.Now()
	done := make(chan error, 1)
	go func() { done <- session.Run(command) }()

	select {
	case <-ctx.Done():
		_ = session.Signal(ssh.SIGKILL)
		return ports.CommandResult{Duration: time.Since(start)}, wrapErr("exec", "", ctx.Err())
	case err := <-done:
		res := ports.CommandResult{
			Stdout:   stdout.String(),
			Stderr:   stderr.String(),
			Duration: time.Since(start),
		}
		if err == nil {
			return res, nil
		}
		if ee, ok := err.(*ssh.ExitError); ok {
			res.ExitCode = ee.ExitStatus()
			return res, nil
		}
		return res, wrapErr("exec", "", err)
	}
}

func (c *connection) Close() error {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.closed {
		return nil
	}
	c.closed = true
	if c.client != nil {
		return c.client.Close()
	}
	return nil
}

func (c *connection) keepAlive() {
	ticker := time.NewTicker(c.cfg.KeepAlivePeriod)
	defer ticker.Stop()
	fails := 0
	for range ticker.C {
		c.mu.Lock()
		closed := c.closed
		client := c.client
		c.mu.Unlock()
		if closed || client == nil {
			return
		}
		_, _, err := client.SendRequest("keepalive@hostdeck", true, nil)
		if err != nil {
			fails++
			if fails >= c.cfg.KeepAliveCount {
				_ = c.Close()
				return
			}
			continue
		}
		fails = 0
	}
}

// jumpConnection owns both bastion and target clients.
type jumpConnection struct {
	*connection
	bastion *ssh.Client
}

func newJumpConnection(target, bastion *ssh.Client, cfg FactoryConfig) *jumpConnection {
	return &jumpConnection{
		connection: newConnection(target, cfg),
		bastion:    bastion,
	}
}

func (j *jumpConnection) Close() error {
	err := j.connection.Close()
	if j.bastion != nil {
		if e := j.bastion.Close(); e != nil && err == nil {
			err = e
		}
	}
	return err
}

// HostKeyStoreAdapter adapts sqlite.HostKeyStore to the local HostKeyStore interface.
type HostKeyStoreAdapter struct {
	GetFn   func(ctx context.Context, host string, port int, keyType string) (fingerprint, publicKey string, err error)
	StoreFn func(ctx context.Context, host string, port int, keyType, fingerprint, publicKey string) error
}

func (a HostKeyStoreAdapter) Lookup(ctx context.Context, host string, port int, keyType string) (string, string, error) {
	return a.GetFn(ctx, host, port, keyType)
}

func (a HostKeyStoreAdapter) Store(ctx context.Context, host string, port int, keyType, fingerprint, publicKey string) error {
	return a.StoreFn(ctx, host, port, keyType, fingerprint, publicKey)
}
