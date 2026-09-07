using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Metric = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    ComparisonOperator = table.Column<int>(type: "INTEGER", nullable: true),
                    ThresholdValue = table.Column<double>(type: "REAL", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeGroup = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ScopeServerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "docker_metric_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CpuPercent = table.Column<double>(type: "REAL", nullable: false),
                    MemoryUsedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryLimitBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    NetworkReceivedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    NetworkTransmittedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    BlockReadBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    BlockWrittenBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_docker_metric_samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CredentialKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CredentialKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MonitoringIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    DockerEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCollectedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    JumpHostAddress = table.Column<string>(type: "TEXT", maxLength: 253, nullable: true),
                    JumpHostPort = table.Column<int>(type: "INTEGER", nullable: true),
                    JumpHostUsername = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    JumpHostCredentialKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    JumpHostCredentialKind = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    InstanceName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DefaultCollectionIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectionTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectionRetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrentCollections = table.Column<int>(type: "INTEGER", nullable: false),
                    MetricRetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    EventRetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    DesktopNotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumNotificationSeverity = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoRefreshEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoRefreshIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageDirectory = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.Id);
                    table.CheckConstraint("ck_settings_single_row", "Id = 1");
                });

            migrationBuilder.CreateTable(
                name: "ssh_host_keys",
                columns: table => new
                {
                    Host = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ApprovedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ssh_host_keys", x => new { x.Host, x.Port });
                });

            migrationBuilder.CreateTable(
                name: "disk_metric_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    MountPoint = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    FileSystem = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UsedBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disk_metric_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_disk_metric_samples_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "docker_containers",
                columns: table => new
                {
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Health = table.Column<int>(type: "INTEGER", nullable: false),
                    RestartCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_docker_containers", x => x.ContainerId);
                    table.ForeignKey(
                        name: "FK_docker_containers_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "docker_hosts",
                columns: table => new
                {
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EngineVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RunningContainers = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalContainers = table.Column<int>(type: "INTEGER", nullable: false),
                    Images = table.Column<int>(type: "INTEGER", nullable: false),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_docker_hosts", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_docker_hosts_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Metric = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    AcknowledgedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RecoveredAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ResolvedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CurrentValue = table.Column<double>(type: "REAL", nullable: true),
                    ThresholdValue = table.Column<double>(type: "REAL", nullable: true),
                    LastNotifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incidents_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metric_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CpuUser = table.Column<double>(type: "REAL", nullable: false),
                    CpuSystem = table.Column<double>(type: "REAL", nullable: false),
                    CpuIoWait = table.Column<double>(type: "REAL", nullable: false),
                    CpuNice = table.Column<double>(type: "REAL", nullable: false),
                    CpuSteal = table.Column<double>(type: "REAL", nullable: false),
                    CoreCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryTotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryUsedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryCachedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryBuffersBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    SwapTotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    SwapUsedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LoadOne = table.Column<double>(type: "REAL", nullable: false),
                    LoadFive = table.Column<double>(type: "REAL", nullable: false),
                    LoadFifteen = table.Column<double>(type: "REAL", nullable: false),
                    UptimeSeconds = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metric_samples_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "network_metric_samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    InterfaceName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReceivedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    TransmittedBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_network_metric_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_network_metric_samples_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_tags",
                columns: table => new
                {
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tag = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_tags", x => new { x.ServerId, x.Tag });
                    table.ForeignKey(
                        name: "FK_server_tags_servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incident_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IncidentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incident_events_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_enabled",
                table: "alert_rules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "ix_disk_metric_samples_server_time",
                table: "disk_metric_samples",
                columns: new[] { "ServerId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_disk_metric_samples_time",
                table: "disk_metric_samples",
                column: "ObservedAt");

            migrationBuilder.CreateIndex(
                name: "ix_docker_containers_server",
                table: "docker_containers",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "ix_docker_metric_samples_container_time",
                table: "docker_metric_samples",
                columns: new[] { "ContainerId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_docker_metric_samples_time",
                table: "docker_metric_samples",
                column: "ObservedAt");

            migrationBuilder.CreateIndex(
                name: "ix_incident_events_incident_time",
                table: "incident_events",
                columns: new[] { "IncidentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_server_rule_status",
                table: "incidents",
                columns: new[] { "ServerId", "RuleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_started_at",
                table: "incidents",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_status",
                table: "incidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_metric_samples_server_time",
                table: "metric_samples",
                columns: new[] { "ServerId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_metric_samples_time",
                table: "metric_samples",
                column: "ObservedAt");

            migrationBuilder.CreateIndex(
                name: "ix_network_metric_samples_server_time",
                table: "network_metric_samples",
                columns: new[] { "ServerId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_network_metric_samples_time",
                table: "network_metric_samples",
                column: "ObservedAt");

            migrationBuilder.CreateIndex(
                name: "ix_server_tags_tag",
                table: "server_tags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "ix_servers_endpoint",
                table: "servers",
                columns: new[] { "Address", "Port" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_servers_group",
                table: "servers",
                column: "GroupName");

            migrationBuilder.CreateIndex(
                name: "ix_servers_status",
                table: "servers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "disk_metric_samples");

            migrationBuilder.DropTable(
                name: "docker_containers");

            migrationBuilder.DropTable(
                name: "docker_hosts");

            migrationBuilder.DropTable(
                name: "docker_metric_samples");

            migrationBuilder.DropTable(
                name: "incident_events");

            migrationBuilder.DropTable(
                name: "metric_samples");

            migrationBuilder.DropTable(
                name: "network_metric_samples");

            migrationBuilder.DropTable(
                name: "server_tags");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "ssh_host_keys");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "servers");
        }
    }
}
