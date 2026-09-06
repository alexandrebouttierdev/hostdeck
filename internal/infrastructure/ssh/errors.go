package ssh

import (
	"errors"
	"fmt"
)

// Typed SSH errors for connection / auth / host-key failures.
var (
	ErrDialFailed           = errors.New("ssh: dial failed")
	ErrAuthenticationFailed = errors.New("ssh: authentication failed")
	ErrHostKeyMismatch      = errors.New("ssh: host key mismatch")
	ErrHostKeyUnknown       = errors.New("ssh: host key unknown")
	ErrJumpHostRequired     = errors.New("ssh: jump host required")
	ErrJumpHostUnavailable  = errors.New("ssh: jump host unavailable")
	ErrTimeout              = errors.New("ssh: timeout")
	ErrClosed               = errors.New("ssh: connection closed")
	ErrNoAuthMethod         = errors.New("ssh: no authentication method available")
)

// Error wraps a typed SSH sentinel with context.
type Error struct {
	Op   string
	Host string
	Err  error
}

func (e *Error) Error() string {
	if e.Host == "" {
		return fmt.Sprintf("ssh %s: %v", e.Op, e.Err)
	}
	return fmt.Sprintf("ssh %s %s: %v", e.Op, e.Host, e.Err)
}

func (e *Error) Unwrap() error { return e.Err }

func wrapErr(op, host string, err error) error {
	if err == nil {
		return nil
	}
	return &Error{Op: op, Host: host, Err: err}
}
