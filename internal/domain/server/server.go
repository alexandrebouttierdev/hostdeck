package server

import (
	"strings"
	"time"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// ServerID identifie un serveur de manière unique.
type ServerID string

func NewServerID() ServerID {
	return ServerID(shared.NewID())
}

func (id ServerID) String() string { return string(id) }

func ParseServerID(raw string) (ServerID, error) {
	if !shared.IsValidID(raw) {
		return "", shared.NewValidationError("id", "invalid server id")
	}
	return ServerID(raw), nil
}

// ServerStatus représente l'état de connexion d'un serveur.
type ServerStatus string

const (
	ServerStatusOnline               ServerStatus = "online"
	ServerStatusOffline              ServerStatus = "offline"
	ServerStatusGatewayUnavailable   ServerStatus = "gateway_unavailable"
	ServerStatusAuthenticationFailed ServerStatus = "authentication_failed"
	ServerStatusWarning              ServerStatus = "warning"
	ServerStatusUnknown              ServerStatus = "unknown"
	ServerStatusMaintenance          ServerStatus = "maintenance"
)

func (s ServerStatus) IsHealthy() bool {
	return s == ServerStatusOnline
}

// ConnectionMode décrit comment HostDeck se connecte à l'hôte.
type ConnectionMode string

const (
	ConnectionModeDirect ConnectionMode = "direct"
	ConnectionModeJump   ConnectionMode = "jump"
)

// AuthMethod décrit la méthode d'authentification SSH.
type AuthMethod string

const (
	AuthMethodKey      AuthMethod = "key"
	AuthMethodPassword AuthMethod = "password"
	AuthMethodAgent    AuthMethod = "agent"
)

// Server représente un hôte surveillé.
type Server struct {
	ID               ServerID
	Name             string
	Host             string
	Port             int
	Username         string
	ConnectionMode   ConnectionMode
	JumpHostID       *ServerID
	AuthMethod       AuthMethod
	CredentialRef    string
	Group            string
	Environment      string
	Role             string
	OSFamily         string
	OSName           string
	Tags             []string
	Status           ServerStatus
	MonitoringEnabled bool
	DockerEnabled    bool
	IntervalSeconds  int
	CreatedAt        time.Time
	UpdatedAt        time.Time
	LastCollectedAt  *time.Time
}

func (s *Server) Validate() error {
	name := strings.TrimSpace(s.Name)
	if name == "" {
		return shared.NewValidationError("name", "name is required")
	}
	if strings.TrimSpace(s.Host) == "" {
		return shared.NewValidationError("host", "host is required")
	}
	if s.Port <= 0 || s.Port > 65535 {
		return shared.NewValidationError("port", "port must be between 1 and 65535")
	}
	if strings.TrimSpace(s.Username) == "" {
		return shared.NewValidationError("username", "username is required")
	}
	if s.ConnectionMode != ConnectionModeDirect && s.ConnectionMode != ConnectionModeJump {
		return shared.NewValidationError("connection_mode", "invalid connection mode")
	}
	if s.ConnectionMode == ConnectionModeJump && (s.JumpHostID == nil || *s.JumpHostID == "") {
		return shared.NewValidationError("jump_host_id", "jump host is required for jump mode")
	}
	if s.IntervalSeconds <= 0 {
		s.IntervalSeconds = 60
	}
	s.Name = name
	return nil
}
