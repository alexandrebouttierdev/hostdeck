using System;
using HostDeck.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HostDeck.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de la base locale HostDeck.
///
/// <para>
/// EF Core possède le schéma, les migrations versionnées et le CRUD des tables relationnelles
/// à faible volume. Les tables de séries temporelles y sont déclarées pour que les migrations
/// les créent, mais leurs lectures et écritures passent par du SQL paramétré brut sur la même
/// connexion : c'est le chemin chaud du produit (§18).
/// </para>
/// </summary>
internal sealed class HostDeckDbContext : DbContext
{
    /// <summary>
    /// Convertit les horodatages en millisecondes Unix, en UTC.
    ///
    /// EF Core stocke un <see cref="DateTimeOffset"/> en texte ISO par défaut. Sur les tables
    /// de métriques, qui comptent des millions de lignes et sont interrogées par plage, un
    /// entier occupe moins de place, s'indexe et se compare plus vite, et se regroupe en
    /// buckets par une simple division (§19).
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, long> UtcMillisecondsConverter = new(
        value => value.ToUniversalTime().ToUnixTimeMilliseconds(),
        value => DateTimeOffset.FromUnixTimeMilliseconds(value));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUtcMillisecondsConverter = new(
        value => value == null ? null : value.Value.ToUniversalTime().ToUnixTimeMilliseconds(),
        value => value == null ? null : DateTimeOffset.FromUnixTimeMilliseconds(value.Value));

    /// <summary>
    /// Convertit les identifiants en texte canonique majuscule à tirets.
    ///
    /// EF Core écrit les <see cref="Guid"/> en TEXT par défaut, mais son format exact
    /// appartient au fournisseur. Le chemin chaud des séries temporelles passe par du SQL
    /// paramétré brut sur la même base : si les deux chemins n'écrivent pas exactement la
    /// même représentation, les clés étrangères échouent silencieusement à la comparaison.
    /// Cette conversion fige le format en majuscules à tirets, que le SQL brut reproduit
    /// à l'identique (§18).
    /// </summary>
    private static readonly ValueConverter<Guid, string> GuidTextConverter = new(
        value => value.ToString("D").ToUpperInvariant(),
        value => Guid.ParseExact(value, "D"));

    private static readonly ValueConverter<Guid?, string?> NullableGuidTextConverter = new(
        value => value.HasValue ? value.Value.ToString("D").ToUpperInvariant() : null,
        value => value == null ? null : Guid.ParseExact(value, "D"));

    public HostDeckDbContext(DbContextOptions<HostDeckDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServerRecord> Servers => Set<ServerRecord>();

    public DbSet<ServerTagRecord> ServerTags => Set<ServerTagRecord>();

    public DbSet<AlertRuleRecord> AlertRules => Set<AlertRuleRecord>();

    public DbSet<IncidentRecord> Incidents => Set<IncidentRecord>();

    public DbSet<IncidentEventRecord> IncidentEvents => Set<IncidentEventRecord>();

    public DbSet<SettingsRecord> Settings => Set<SettingsRecord>();

    public DbSet<SshHostKeyRecord> SshHostKeys => Set<SshHostKeyRecord>();

    public DbSet<MetricSampleRecord> MetricSamples => Set<MetricSampleRecord>();

    public DbSet<DiskMetricSampleRecord> DiskMetricSamples => Set<DiskMetricSampleRecord>();

    public DbSet<NetworkMetricSampleRecord> NetworkMetricSamples => Set<NetworkMetricSampleRecord>();

    public DbSet<DockerHostRecord> DockerHosts => Set<DockerHostRecord>();

    public DbSet<DockerContainerRecord> DockerContainers => Set<DockerContainerRecord>();

    public DbSet<DockerMetricSampleRecord> DockerMetricSamples => Set<DockerMetricSampleRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureServers(modelBuilder);
        ConfigureAlerts(modelBuilder);
        ConfigureIncidents(modelBuilder);
        ConfigureSettings(modelBuilder);
        ConfigureMetrics(modelBuilder);
        ConfigureDocker(modelBuilder);

        ApplyUtcMillisecondConversions(modelBuilder);
        ApplyGuidConversions(modelBuilder);
    }

    /// <summary>
    /// Applique la conversion en texte canonique à tous les identifiants du modèle.
    ///
    /// Globale plutôt que propriété par propriété : un oubli produirait une colonne au format
    /// différent des autres et casserait les jointures entre le CRUD EF et le SQL brut.
    /// </summary>
    private static void ApplyGuidConversions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Guid))
                {
                    property.SetValueConverter(GuidTextConverter);
                }
                else if (property.ClrType == typeof(Guid?))
                {
                    property.SetValueConverter(NullableGuidTextConverter);
                }
            }
        }
    }

    private static void ConfigureServers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerRecord>(entity =>
        {
            entity.ToTable("servers");
            entity.HasKey(record => record.Id);

            entity.Property(record => record.Name).HasMaxLength(128).IsRequired();
            entity.Property(record => record.Address).HasMaxLength(253).IsRequired();
            entity.Property(record => record.Username).HasMaxLength(32).IsRequired();
            entity.Property(record => record.CredentialKey).HasMaxLength(128).IsRequired();
            entity.Property(record => record.GroupName).HasMaxLength(64);
            entity.Property(record => record.JumpHostAddress).HasMaxLength(253);
            entity.Property(record => record.JumpHostUsername).HasMaxLength(32);
            entity.Property(record => record.JumpHostCredentialKey).HasMaxLength(128);

            // L'inventaire est trié et filtré sur ces colonnes à chaque affichage.
            entity.HasIndex(record => new { record.Address, record.Port })
                .HasDatabaseName("ix_servers_endpoint")
                .IsUnique();
            entity.HasIndex(record => record.GroupName).HasDatabaseName("ix_servers_group");
            entity.HasIndex(record => record.Status).HasDatabaseName("ix_servers_status");

            entity.HasMany(record => record.Tags)
                .WithOne()
                .HasForeignKey(tag => tag.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServerTagRecord>(entity =>
        {
            entity.ToTable("server_tags");
            entity.HasKey(record => new { record.ServerId, record.Tag });
            entity.Property(record => record.Tag).HasMaxLength(32).IsRequired();

            entity.HasIndex(record => record.Tag).HasDatabaseName("ix_server_tags_tag");
        });
    }

    private static void ConfigureAlerts(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<AlertRuleRecord>(entity =>
        {
            entity.ToTable("alert_rules");
            entity.HasKey(record => record.Id);

            entity.Property(record => record.Name).HasMaxLength(128).IsRequired();
            entity.Property(record => record.Description).HasMaxLength(512);
            entity.Property(record => record.ScopeGroup).HasMaxLength(64);

            // Le moteur d'évaluation ne lit que les règles actives, à chaque cycle.
            entity.HasIndex(record => record.IsEnabled).HasDatabaseName("ix_alert_rules_enabled");
        });

    private static void ConfigureIncidents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IncidentRecord>(entity =>
        {
            entity.ToTable("incidents");
            entity.HasKey(record => record.Id);

            entity.Property(record => record.AcknowledgedBy).HasMaxLength(128);

            // Recherche de l'incident actif d'une règle sur un serveur : c'est ce qui évite
            // d'ouvrir un nouvel incident à chaque cycle tant que la condition dure (§20).
            entity.HasIndex(record => new { record.ServerId, record.RuleId, record.Status })
                .HasDatabaseName("ix_incidents_server_rule_status");
            entity.HasIndex(record => record.Status).HasDatabaseName("ix_incidents_status");
            entity.HasIndex(record => record.StartedAt).HasDatabaseName("ix_incidents_started_at");

            entity.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(record => record.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IncidentEventRecord>(entity =>
        {
            entity.ToTable("incident_events");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedOnAdd();

            entity.Property(record => record.Detail).HasMaxLength(1024);
            entity.Property(record => record.Actor).HasMaxLength(128);

            entity.HasIndex(record => new { record.IncidentId, record.OccurredAt })
                .HasDatabaseName("ix_incident_events_incident_time");

            entity.HasOne<IncidentRecord>()
                .WithMany()
                .HasForeignKey(record => record.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SettingsRecord>(entity =>
        {
            entity.ToTable("settings", table =>
                // Verrou de ligne unique : les paramètres sont globaux, et plusieurs lignes
                // créeraient une ambiguïté silencieuse sur celle qui fait foi.
                table.HasCheckConstraint("ck_settings_single_row", "Id = 1"));

            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedNever();
            entity.Property(record => record.InstanceName).HasMaxLength(64).IsRequired();
            entity.Property(record => record.Description).HasMaxLength(200);
            entity.Property(record => record.StorageDirectory).HasMaxLength(4096);
        });

        modelBuilder.Entity<SshHostKeyRecord>(entity =>
        {
            entity.ToTable("ssh_host_keys");
            entity.HasKey(record => new { record.Host, record.Port });
            entity.Property(record => record.Host).HasMaxLength(253).IsRequired();
            entity.Property(record => record.Fingerprint).HasMaxLength(256).IsRequired();
        });
    }

    private static void ConfigureMetrics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetricSampleRecord>(entity =>
        {
            entity.ToTable("metric_samples");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedOnAdd();

            // Index déterminant du produit : toute lecture d'historique est une plage
            // temporelle pour un serveur, et toute purge une plage temporelle globale.
            entity.HasIndex(record => new { record.ServerId, record.ObservedAt })
                .HasDatabaseName("ix_metric_samples_server_time");
            entity.HasIndex(record => record.ObservedAt)
                .HasDatabaseName("ix_metric_samples_time");

            entity.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(record => record.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DiskMetricSampleRecord>(entity =>
        {
            entity.ToTable("disk_metric_samples");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedOnAdd();
            entity.Property(record => record.MountPoint).HasMaxLength(4096).IsRequired();
            entity.Property(record => record.FileSystem).HasMaxLength(256);

            entity.HasIndex(record => new { record.ServerId, record.ObservedAt })
                .HasDatabaseName("ix_disk_metric_samples_server_time");
            entity.HasIndex(record => record.ObservedAt)
                .HasDatabaseName("ix_disk_metric_samples_time");

            entity.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(record => record.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NetworkMetricSampleRecord>(entity =>
        {
            entity.ToTable("network_metric_samples");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedOnAdd();
            entity.Property(record => record.InterfaceName).HasMaxLength(64).IsRequired();

            entity.HasIndex(record => new { record.ServerId, record.ObservedAt })
                .HasDatabaseName("ix_network_metric_samples_server_time");
            entity.HasIndex(record => record.ObservedAt)
                .HasDatabaseName("ix_network_metric_samples_time");

            entity.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(record => record.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureDocker(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DockerHostRecord>(entity =>
        {
            entity.ToTable("docker_hosts");
            entity.HasKey(record => record.ServerId);
            entity.Property(record => record.EngineVersion).HasMaxLength(64).IsRequired();

            entity.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(record => record.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DockerContainerRecord>(entity =>
        {
            entity.ToTable("docker_containers");
            entity.HasKey(record => record.ContainerId);
            entity.Property(record => record.ContainerId).HasMaxLength(64);
            entity.Property(record => record.Name).HasMaxLength(255).IsRequired();
            entity.Property(record => record.Image).HasMaxLength(512).IsRequired();

            entity.HasIndex(record => record.ServerId).HasDatabaseName("ix_docker_containers_server");

            entity.HasOne<ServerRecord>()
                .WithMany()
                .HasForeignKey(record => record.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DockerMetricSampleRecord>(entity =>
        {
            entity.ToTable("docker_metric_samples");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).ValueGeneratedOnAdd();
            entity.Property(record => record.ContainerId).HasMaxLength(64).IsRequired();

            entity.HasIndex(record => new { record.ContainerId, record.ObservedAt })
                .HasDatabaseName("ix_docker_metric_samples_container_time");
            entity.HasIndex(record => record.ObservedAt)
                .HasDatabaseName("ix_docker_metric_samples_time");
        });
    }

    /// <summary>
    /// Applique la conversion en millisecondes Unix à tous les horodatages du modèle.
    ///
    /// Appliquée globalement plutôt que propriété par propriété : un oubli produirait une
    /// colonne au format différent des autres, que les requêtes de plage compareraient
    /// silencieusement de travers.
    /// </summary>
    private static void ApplyUtcMillisecondConversions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(UtcMillisecondsConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableUtcMillisecondsConverter);
                }
            }
        }
    }
}
