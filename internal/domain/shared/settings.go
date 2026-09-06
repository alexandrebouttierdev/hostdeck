package shared

import "time"

// Settings contient la configuration globale HostDeck.
type Settings struct {
	InstanceName            string
	Description             string
	Timezone                string
	Language                string
	AutoStart               bool
	DefaultIntervalSeconds  int
	RequestTimeoutSeconds   int
	RetryAttempts           int
	ParallelCollection      bool
	AutoDiscovery           bool
	AvailabilityCheck       bool
	PingBeforeCollect       bool
	DelayBetweenHostsSec    int
	Theme                   string
	DisplayDensity          string
	ChartStyle              string
	AutoRefresh             bool
	RefreshIntervalSeconds  int
	AnimationsEnabled       bool
	NumberFormat            string
	TemperatureUnit         string
	AuthRequired            bool
	AutoSession             bool
	SessionDurationHours    int
	DefaultRole             string
	AccessLogging           bool
	DataEncryption          bool
	MetricsRetentionDays    int
	EventsRetentionDays     int
	MaxDiskGB               int
	AutoCleanup             bool
	CompressMetrics         bool
	StoragePath             string
	NotifyEmail             bool
	NotifySlack             bool
	NotifyTeams             bool
	NotifyWebhook           bool
	NotifyDesktop           bool
	UpdatedAt               time.Time
}

func DefaultSettings() Settings {
	return Settings{
		InstanceName:           "HostDeck Production",
		Description:            "Supervision de l'infrastructure de production",
		Timezone:               "Europe/Paris",
		Language:               "fr",
		AutoStart:              true,
		DefaultIntervalSeconds: 60,
		RequestTimeoutSeconds:  10,
		RetryAttempts:          3,
		ParallelCollection:     true,
		AutoDiscovery:          true,
		AvailabilityCheck:      true,
		Theme:                  "dark",
		DisplayDensity:         "compact",
		ChartStyle:             "lines",
		AutoRefresh:            true,
		RefreshIntervalSeconds: 30,
		AnimationsEnabled:      true,
		NumberFormat:           "auto",
		TemperatureUnit:        "celsius",
		AutoSession:            true,
		SessionDurationHours:   24,
		DefaultRole:            "reader",
		AccessLogging:          true,
		DataEncryption:         true,
		MetricsRetentionDays:   90,
		EventsRetentionDays:    180,
		MaxDiskGB:              100,
		AutoCleanup:            true,
		CompressMetrics:        true,
		StoragePath:            "/var/lib/hostdeck",
		NotifyEmail:            true,
		NotifyDesktop:          true,
		UpdatedAt:              time.Now().UTC(),
	}
}
