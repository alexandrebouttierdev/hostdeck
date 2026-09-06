// Package docker implements Docker container listing use cases.
package docker

import (
	"context"

	"github.com/alexandrebouttierdev/hostdeck/internal/application/dto"
	"github.com/alexandrebouttierdev/hostdeck/internal/application/ports"
	dockerdomain "github.com/alexandrebouttierdev/hostdeck/internal/domain/docker"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// Service groups Docker use cases.
type Service struct {
	runtime ports.ContainerRuntime
}

// NewService constructs a Docker Service.
func NewService(runtime ports.ContainerRuntime) *Service {
	return &Service{runtime: runtime}
}

// ListContainers lists containers for a server via ContainerRuntime.
func (s *Service) ListContainers(ctx context.Context, serverID string) ([]dto.DockerContainerSummaryDTO, error) {
	if s.runtime == nil {
		return nil, shared.NewValidationError("runtime", "container runtime not configured")
	}
	id, err := server.ParseServerID(serverID)
	if err != nil {
		return nil, shared.NewValidationError("server_id", "invalid server id")
	}
	containers, err := s.runtime.ListContainers(ctx, id)
	if err != nil {
		return nil, err
	}
	out := make([]dto.DockerContainerSummaryDTO, 0, len(containers))
	for _, c := range containers {
		out = append(out, dto.FromContainer(c))
	}
	return out, nil
}

// GetInfo returns Docker host info.
func (s *Service) GetInfo(ctx context.Context, serverID string) (dto.DockerHostInfoDTO, error) {
	if s.runtime == nil {
		return dto.DockerHostInfoDTO{}, shared.NewValidationError("runtime", "container runtime not configured")
	}
	id, err := server.ParseServerID(serverID)
	if err != nil {
		return dto.DockerHostInfoDTO{}, shared.NewValidationError("server_id", "invalid server id")
	}
	info, err := s.runtime.Info(ctx, id)
	if err != nil {
		return dto.DockerHostInfoDTO{}, err
	}
	return dto.FromDockerHostInfo(info), nil
}

// GetContainer returns container details.
func (s *Service) GetContainer(ctx context.Context, serverID, containerID string) (dto.DockerContainerDetailsDTO, error) {
	if s.runtime == nil {
		return dto.DockerContainerDetailsDTO{}, shared.NewValidationError("runtime", "container runtime not configured")
	}
	sid, err := server.ParseServerID(serverID)
	if err != nil {
		return dto.DockerContainerDetailsDTO{}, shared.NewValidationError("server_id", "invalid server id")
	}
	c, err := s.runtime.GetContainer(ctx, sid, dockerdomain.ContainerID(containerID))
	if err != nil {
		return dto.DockerContainerDetailsDTO{}, err
	}
	return dto.FromContainerDetails(c), nil
}
