package sqlite

import (
	"context"
	"database/sql"
	"errors"
	"fmt"

	"github.com/alexandrebouttierdev/hostdeck/internal/domain/server"
	"github.com/alexandrebouttierdev/hostdeck/internal/domain/shared"
)

// ServerRepository persists servers in SQLite.
type ServerRepository struct {
	db *DB
}

// NewServerRepository constructs a ServerRepository.
func NewServerRepository(db *DB) *ServerRepository {
	return &ServerRepository{db: db}
}

type serverRow struct {
	ID                string
	Name              string
	Host              string
	Port              int
	Username          string
	ConnectionMode    string
	JumpHostID        sql.NullString
	AuthMethod        string
	CredentialRef     string
	GroupName         string
	Environment       string
	Role              string
	OSFamily          string
	OSName            string
	Status            string
	MonitoringEnabled int
	DockerEnabled     int
	IntervalSeconds   int
	CreatedAt         string
	UpdatedAt         string
	LastCollectedAt   sql.NullString
}

func (r *ServerRepository) List(ctx context.Context) ([]server.Server, error) {
	rows, err := r.db.sql.QueryContext(ctx, `
SELECT id, name, host, port, username, connection_mode, jump_host_id, auth_method,
       credential_ref, group_name, environment, role, os_family, os_name, status,
       monitoring_enabled, docker_enabled, interval_seconds, created_at, updated_at, last_collected_at
FROM servers
ORDER BY name COLLATE NOCASE, id`)
	if err != nil {
		return nil, fmt.Errorf("list servers: %w", err)
	}
	defer rows.Close()

	var out []server.Server
	for rows.Next() {
		srv, err := scanServer(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, srv)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}

	for i := range out {
		tags, err := r.loadTags(ctx, out[i].ID)
		if err != nil {
			return nil, err
		}
		out[i].Tags = tags
	}
	return out, nil
}

func (r *ServerRepository) Get(ctx context.Context, id server.ServerID) (server.Server, error) {
	row := r.db.sql.QueryRowContext(ctx, `
SELECT id, name, host, port, username, connection_mode, jump_host_id, auth_method,
       credential_ref, group_name, environment, role, os_family, os_name, status,
       monitoring_enabled, docker_enabled, interval_seconds, created_at, updated_at, last_collected_at
FROM servers WHERE id = ?`, id.String())

	srv, err := scanServer(row)
	if errors.Is(err, sql.ErrNoRows) {
		return server.Server{}, shared.ErrNotFound
	}
	if err != nil {
		return server.Server{}, fmt.Errorf("get server: %w", err)
	}

	tags, err := r.loadTags(ctx, id)
	if err != nil {
		return server.Server{}, err
	}
	srv.Tags = tags
	return srv, nil
}

func (r *ServerRepository) Create(ctx context.Context, srv server.Server) error {
	if err := srv.Validate(); err != nil {
		return err
	}

	tx, err := r.db.sql.BeginTx(ctx, nil)
	if err != nil {
		return err
	}
	defer func() { _ = tx.Rollback() }()

	var jumpID any
	if srv.JumpHostID != nil {
		jumpID = srv.JumpHostID.String()
	}

	_, err = tx.ExecContext(ctx, `
INSERT INTO servers (
	id, name, host, port, username, connection_mode, jump_host_id, auth_method,
	credential_ref, group_name, environment, role, os_family, os_name, status,
	monitoring_enabled, docker_enabled, interval_seconds, created_at, updated_at, last_collected_at
) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		srv.ID.String(),
		srv.Name,
		srv.Host,
		srv.Port,
		srv.Username,
		string(srv.ConnectionMode),
		jumpID,
		string(srv.AuthMethod),
		srv.CredentialRef,
		srv.Group,
		srv.Environment,
		srv.Role,
		srv.OSFamily,
		srv.OSName,
		string(srv.Status),
		boolToInt(srv.MonitoringEnabled),
		boolToInt(srv.DockerEnabled),
		srv.IntervalSeconds,
		formatTime(srv.CreatedAt),
		formatTime(srv.UpdatedAt),
		nullTime(srv.LastCollectedAt),
	)
	if err != nil {
		return fmt.Errorf("create server: %w", err)
	}

	if err := replaceTagsTx(ctx, tx, srv.ID, srv.Tags); err != nil {
		return err
	}
	return tx.Commit()
}

func (r *ServerRepository) Update(ctx context.Context, srv server.Server) error {
	if err := srv.Validate(); err != nil {
		return err
	}

	tx, err := r.db.sql.BeginTx(ctx, nil)
	if err != nil {
		return err
	}
	defer func() { _ = tx.Rollback() }()

	var jumpID any
	if srv.JumpHostID != nil {
		jumpID = srv.JumpHostID.String()
	}

	res, err := tx.ExecContext(ctx, `
UPDATE servers SET
	name = ?, host = ?, port = ?, username = ?, connection_mode = ?, jump_host_id = ?,
	auth_method = ?, credential_ref = ?, group_name = ?, environment = ?, role = ?,
	os_family = ?, os_name = ?, status = ?, monitoring_enabled = ?, docker_enabled = ?,
	interval_seconds = ?, updated_at = ?, last_collected_at = ?
WHERE id = ?`,
		srv.Name,
		srv.Host,
		srv.Port,
		srv.Username,
		string(srv.ConnectionMode),
		jumpID,
		string(srv.AuthMethod),
		srv.CredentialRef,
		srv.Group,
		srv.Environment,
		srv.Role,
		srv.OSFamily,
		srv.OSName,
		string(srv.Status),
		boolToInt(srv.MonitoringEnabled),
		boolToInt(srv.DockerEnabled),
		srv.IntervalSeconds,
		formatTime(srv.UpdatedAt),
		nullTime(srv.LastCollectedAt),
		srv.ID.String(),
	)
	if err != nil {
		return fmt.Errorf("update server: %w", err)
	}
	n, err := res.RowsAffected()
	if err != nil {
		return err
	}
	if n == 0 {
		return shared.ErrNotFound
	}

	if err := replaceTagsTx(ctx, tx, srv.ID, srv.Tags); err != nil {
		return err
	}
	return tx.Commit()
}

func (r *ServerRepository) Delete(ctx context.Context, id server.ServerID) error {
	res, err := r.db.sql.ExecContext(ctx, `DELETE FROM servers WHERE id = ?`, id.String())
	if err != nil {
		return fmt.Errorf("delete server: %w", err)
	}
	n, err := res.RowsAffected()
	if err != nil {
		return err
	}
	if n == 0 {
		return shared.ErrNotFound
	}
	return nil
}

func (r *ServerRepository) loadTags(ctx context.Context, id server.ServerID) ([]string, error) {
	rows, err := r.db.sql.QueryContext(ctx,
		`SELECT tag FROM server_tags WHERE server_id = ? ORDER BY tag COLLATE NOCASE`, id.String())
	if err != nil {
		return nil, fmt.Errorf("load tags: %w", err)
	}
	defer rows.Close()

	var tags []string
	for rows.Next() {
		var tag string
		if err := rows.Scan(&tag); err != nil {
			return nil, err
		}
		tags = append(tags, tag)
	}
	return tags, rows.Err()
}

func replaceTagsTx(ctx context.Context, tx *sql.Tx, id server.ServerID, tags []string) error {
	if _, err := tx.ExecContext(ctx, `DELETE FROM server_tags WHERE server_id = ?`, id.String()); err != nil {
		return fmt.Errorf("clear tags: %w", err)
	}
	for _, tag := range tags {
		if tag == "" {
			continue
		}
		if _, err := tx.ExecContext(ctx,
			`INSERT INTO server_tags (server_id, tag) VALUES (?, ?)`, id.String(), tag); err != nil {
			return fmt.Errorf("insert tag: %w", err)
		}
	}
	return nil
}

type scannable interface {
	Scan(dest ...any) error
}

func scanServer(row scannable) (server.Server, error) {
	var rec serverRow
	err := row.Scan(
		&rec.ID, &rec.Name, &rec.Host, &rec.Port, &rec.Username, &rec.ConnectionMode,
		&rec.JumpHostID, &rec.AuthMethod, &rec.CredentialRef, &rec.GroupName,
		&rec.Environment, &rec.Role, &rec.OSFamily, &rec.OSName, &rec.Status,
		&rec.MonitoringEnabled, &rec.DockerEnabled, &rec.IntervalSeconds,
		&rec.CreatedAt, &rec.UpdatedAt, &rec.LastCollectedAt,
	)
	if err != nil {
		return server.Server{}, err
	}

	createdAt, err := parseTime(rec.CreatedAt)
	if err != nil {
		return server.Server{}, fmt.Errorf("parse created_at: %w", err)
	}
	updatedAt, err := parseTime(rec.UpdatedAt)
	if err != nil {
		return server.Server{}, fmt.Errorf("parse updated_at: %w", err)
	}
	lastCollected, err := parseNullTime(rec.LastCollectedAt)
	if err != nil {
		return server.Server{}, fmt.Errorf("parse last_collected_at: %w", err)
	}

	srv := server.Server{
		ID:                server.ServerID(rec.ID),
		Name:              rec.Name,
		Host:              rec.Host,
		Port:              rec.Port,
		Username:          rec.Username,
		ConnectionMode:    server.ConnectionMode(rec.ConnectionMode),
		AuthMethod:        server.AuthMethod(rec.AuthMethod),
		CredentialRef:     rec.CredentialRef,
		Group:             rec.GroupName,
		Environment:       rec.Environment,
		Role:              rec.Role,
		OSFamily:          rec.OSFamily,
		OSName:            rec.OSName,
		Status:            server.ServerStatus(rec.Status),
		MonitoringEnabled: intToBool(rec.MonitoringEnabled),
		DockerEnabled:     intToBool(rec.DockerEnabled),
		IntervalSeconds:   rec.IntervalSeconds,
		CreatedAt:         createdAt,
		UpdatedAt:         updatedAt,
		LastCollectedAt:   lastCollected,
	}
	if rec.JumpHostID.Valid && rec.JumpHostID.String != "" {
		jid := server.ServerID(rec.JumpHostID.String)
		srv.JumpHostID = &jid
	}
	return srv, nil
}
