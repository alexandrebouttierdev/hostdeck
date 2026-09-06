// Package dto contains application data transfer objects for the HostDeck UI.
package dto

import "time"

// CreateServerDTO is the payload to add a monitored host.
type CreateServerDTO struct {
	Name              string
	Host              string
	Port              int
	Username          string
	ConnectionMode    string
	JumpHostID        string
	AuthMethod        string
	CredentialSecret  string // optional; stored in CredentialStore, not SQLite
	Group             string
	Environment       string
	Role              string
	Tags              []string
	MonitoringEnabled bool
	DockerEnabled     bool
	IntervalSeconds   int
}

// UpdateServerDTO is the payload to update a monitored host.
type UpdateServerDTO struct {
	ID                string
	Name              string
	Host              string
	Port              int
	Username          string
	ConnectionMode    string
	JumpHostID        string
	AuthMethod        string
	CredentialSecret  string // optional; empty means keep existing
	Group             string
	Environment       string
	Role              string
	Tags              []string
	Status            string
	MonitoringEnabled bool
	DockerEnabled     bool
	IntervalSeconds   int
}

// ServerSummaryDTO is a dense row for infrastructure tables.
type ServerSummaryDTO struct {
	ID                string
	Name              string
	Host              string
	Port              int
	OSName            string
	OSFamily          string
	Group             string
	Environment       string
	Role              string
	Tags              []string
	Status            string
	MonitoringEnabled bool
	DockerEnabled     bool
	ConnectionMode    string
	JumpHostID        string

	CPUPercent    float64
	MemoryPercent float64
	DiskPercent   float64
	Load1         float64
	NetworkRXMbps float64
	NetworkTXMbps float64
	UptimeSeconds uint64
	LastCollected *time.Time

	CPUSparkline     []float64
	MemorySparkline  []float64
	DiskSparkline    []float64
	NetworkSparkline []float64
}

// ServerDetailsDTO is the full host detail view.
type ServerDetailsDTO struct {
	ServerSummaryDTO
	Username        string
	AuthMethod      string
	CredentialRef   string
	IntervalSeconds int
	CreatedAt       time.Time
	UpdatedAt       time.Time
	LatestMetrics   *LatestMetricDTO
}

// ServerConfigurationDTO exposes editable connection settings.
type ServerConfigurationDTO struct {
	ID                string
	Name              string
	Host              string
	Port              int
	Username          string
	ConnectionMode    string
	JumpHostID        string
	AuthMethod        string
	Group             string
	Environment       string
	Role              string
	Tags              []string
	MonitoringEnabled bool
	DockerEnabled     bool
	IntervalSeconds   int
}

// TestConnectionRequestDTO requests an SSH connectivity test.
type TestConnectionRequestDTO struct {
	ServerID string
}

// TestConnectionResultDTO is the outcome of an SSH test.
type TestConnectionResultDTO struct {
	Success    bool
	LatencyMs  int64
	Message    string
	RemoteHost string
	ErrorCode  string
}

// LatestMetricDTO is the most recent sample for a host.
type LatestMetricDTO struct {
	ServerID             string
	CollectedAt          time.Time
	CPUUserPercent       float64
	CPUSystemPercent     float64
	CPUIOWaitPercent     float64
	CPUNicePercent       float64
	CPUStealPercent      float64
	CPUTotalPercent      float64
	MemoryUsedBytes      uint64
	MemoryCacheBytes     uint64
	MemoryBufferBytes    uint64
	MemoryFreeBytes      uint64
	MemoryTotalBytes     uint64
	MemoryUsedPercent    float64
	SwapUsedBytes        uint64
	SwapTotalBytes       uint64
	SwapUsedPercent      float64
	Load1                float64
	Load5                float64
	Load15               float64
	NetworkRXBytesPerSec float64
	NetworkTXBytesPerSec float64
	DiskUsedBytes        uint64
	DiskTotalBytes       uint64
	DiskUsedPercent      float64
	DiskReadBytesPerSec  float64
	DiskWriteBytesPerSec float64
	UptimeSeconds        uint64
}

// MetricHistoryRequestDTO selects a history window.
type MetricHistoryRequestDTO struct {
	ServerID string
	Range    string // e.g. "15m", "1h", "6h"
	From     *time.Time
	To       *time.Time
	Limit    int
}

// MetricPointDTO is one history point.
type MetricPointDTO struct {
	CollectedAt          time.Time
	CPUTotalPercent      float64
	MemoryUsedPercent    float64
	DiskUsedPercent      float64
	Load1                float64
	NetworkRXBytesPerSec float64
	NetworkTXBytesPerSec float64
}

// MetricHistoryDTO is a time series for charts.
type MetricHistoryDTO struct {
	ServerID string
	From     time.Time
	To       time.Time
	Points   []MetricPointDTO
}

// MetricStatisticsDTO summarizes a history window.
type MetricStatisticsDTO struct {
	ServerID   string
	CPUAvg     float64
	CPUPeak    float64
	MemoryAvg  float64
	MemoryPeak float64
	DiskAvg    float64
	DiskPeak   float64
	LoadAvg    float64
	LoadPeak   float64
}

// DockerContainerSummaryDTO is a container list row.
type DockerContainerSummaryDTO struct {
	ID            string
	ServerID      string
	Name          string
	Image         string
	Status        string
	State         string
	Ports         string
	CPUPercent    float64
	MemoryUsed    uint64
	MemoryLimit   uint64
	MemoryPercent float64
	RestartCount  int
}

// DockerContainerDetailsDTO is a full container view.
type DockerContainerDetailsDTO struct {
	DockerContainerSummaryDTO
	Networks   string
	Command    string
	CreatedAt  time.Time
	StartedAt  *time.Time
	NetworkRX  float64
	NetworkTX  float64
	BlockRead  float64
	BlockWrite float64
}

// DockerContainerStatsDTO holds live container resource stats.
type DockerContainerStatsDTO struct {
	ID          string
	ServerID    string
	CPUPercent  float64
	MemoryUsed  uint64
	MemoryLimit uint64
	NetworkRX   float64
	NetworkTX   float64
	BlockRead   float64
	BlockWrite  float64
}

// DockerHostInfoDTO summarizes Docker on a host.
type DockerHostInfoDTO struct {
	ServerID          string
	DockerVersion     string
	APIVersion        string
	OS                string
	Architecture      string
	Containers        int
	ContainersRunning int
	Images            int
	NCPU              int
	MemTotal          uint64
}

// IncidentDTO is an incident list row.
type IncidentDTO struct {
	ID             string
	DisplayID      string
	ServerID       string
	ServerName     string
	Metric         string
	Problem        string
	Severity       string
	RuleID         string
	RuleName       string
	Status         string
	CurrentValue   float64
	Threshold      float64
	StartedAt      time.Time
	LastUpdatedAt  time.Time
	DurationSecs   int64
	AcknowledgedAt *time.Time
	AcknowledgedBy string
	RecoveredAt    *time.Time
	ResolvedAt     *time.Time
}

// IncidentDetailsDTO is the incident diagnostic panel.
type IncidentDetailsDTO struct {
	IncidentDTO
	Notes string
}

// AcknowledgeIncidentDTO acknowledges an open incident.
type AcknowledgeIncidentDTO struct {
	IncidentID string
	By         string
	Notes      string
}

// ResolveIncidentDTO resolves an incident.
type ResolveIncidentDTO struct {
	IncidentID string
	Notes      string
}

// AlertRuleDTO is an alert rule for CRUD UIs.
type AlertRuleDTO struct {
	ID              string
	Name            string
	Enabled         bool
	Metric          string
	Operator        string
	Threshold       float64
	DurationSeconds int
	Severity        string
	CooldownSeconds int
	Scope           string
	Category        string
	Expression      string
	LastTriggeredAt *time.Time
	Status          string
	CreatedAt       time.Time
	UpdatedAt       time.Time
}

// SettingsDTO mirrors global application settings.
type SettingsDTO struct {
	InstanceName           string
	Description            string
	Timezone               string
	Language               string
	AutoStart              bool
	DefaultIntervalSeconds int
	RequestTimeoutSeconds  int
	RetryAttempts          int
	ParallelCollection     bool
	AutoDiscovery          bool
	AvailabilityCheck      bool
	PingBeforeCollect      bool
	DelayBetweenHostsSec   int
	Theme                  string
	DisplayDensity         string
	ChartStyle             string
	AutoRefresh            bool
	RefreshIntervalSeconds int
	AnimationsEnabled      bool
	NumberFormat           string
	TemperatureUnit        string
	AuthRequired           bool
	AutoSession            bool
	SessionDurationHours   int
	DefaultRole            string
	AccessLogging          bool
	DataEncryption         bool
	MetricsRetentionDays   int
	EventsRetentionDays    int
	MaxDiskGB              int
	AutoCleanup            bool
	CompressMetrics        bool
	StoragePath            string
	NotifyEmail            bool
	NotifySlack            bool
	NotifyTeams            bool
	NotifyWebhook          bool
	NotifyDesktop          bool
	UpdatedAt              time.Time
}

// OverviewDTO aggregates fleet health for the overview screen.
type OverviewDTO struct {
	TotalHosts          int
	OnlineHosts         int
	WarningHosts        int
	ProblemHosts        int
	MaintenanceHosts    int
	OfflineHosts        int
	ActiveIncidents     int
	CriticalIncidents   int
	HighIncidents       int
	WarningIncidents    int
	AverageIncidents    int
	OpenAlertRules      int
	AvailabilityPct     float64
	GlobalCPUPercent    float64
	GlobalMemoryPercent float64
	GlobalDiskPercent   float64
	CollectionActive    bool
	TopHosts            []ServerSummaryDTO
	RecentIncidents     []IncidentDTO
}
