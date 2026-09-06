# HostDeck — Prompt complet de création en Go + Fyne

Tu dois concevoir et développer entièrement **HostDeck**, une application desktop open source de supervision de plusieurs VPS, serveurs Linux et workloads Docker.

Repository officiel :

`https://github.com/alexandrebouttierdev/hostdeck`

Travaille directement dans ce repository. Ne crée pas de projet parallèle.

Les **maquettes/images de référence fournies avec cette spécification sont la source de vérité visuelle principale**.

Elles définissent :

- le rail vertical ;
- la densité ;
- la hiérarchie ;
- les tables ;
- les panneaux ;
- les couleurs de statut ;
- les incidents ;
- les métriques ;
- les graphiques ;
- les sparklines ;
- les interactions principales.

HostDeck doit reprendre très fidèlement cette direction tout en gardant sa propre identité.

---

# 0. Fichiers de maquettes joints — SOURCE DE VÉRITÉ VISUELLE

Le package fourni avec ce prompt contient les maquettes finales validées dans le dossier :

`./mockups/`

Tu dois **ouvrir, examiner et comprendre chacune de ces images avant de commencer l’implémentation de l’UI**.

Fichiers :

```text
mockups/
├── 01_overview.png
├── 02_infrastructure_hosts.png
├── 03_host_details.png
├── 04_active_incidents.png
├── 05_alert_rules.png
├── 06_reports.png
├── 07_topology.png
├── 08_live_data.png
└── 09_settings.png
```

Correspondance fonctionnelle :

| Fichier | Écran HostDeck |
|---|---|
| `01_overview.png` | Vue d’ensemble / état global de l’infrastructure |
| `02_infrastructure_hosts.png` | Infrastructure / liste dense de tous les hôtes |
| `03_host_details.png` | Détail complet d’un hôte et métriques |
| `04_active_incidents.png` | Incidents actifs + panneau de diagnostic |
| `05_alert_rules.png` | Règles d’alerte + édition d’une règle |
| `06_reports.png` | Rapports, SLA, disponibilité, capacité |
| `07_topology.png` | Cartographie / topologie de l’infrastructure |
| `08_live_data.png` | Données en direct / métriques temps réel |
| `09_settings.png` | Paramètres globaux |

Une planche récapitulative est également fournie :

`./MOCKUPS_CONTACT_SHEET.png`

Une référence supplémentaire du style de graphiques souhaité est fournie dans :

`./references/netdata_charts_reference.webp`

Cette image Netdata sert **uniquement de référence pour le langage visuel des graphiques** :
- densité ;
- séries temporelles ;
- stacked areas ;
- grilles ;
- légendes compactes ;
- couleurs techniques ;
- lecture temps réel.

Elle ne doit pas être copiée pixel-perfect et ne définit pas le reste de l’interface.

## Règles de fidélité UI

Les neuf maquettes HostDeck sont prioritaires sur toute interprétation générique du design.

En cas de conflit entre :
1. une convention Fyne par défaut ;
2. une préférence personnelle ;
3. une décision automatique de l’agent ;
4. une maquette fournie ;

**la maquette fournie gagne**, sauf impossibilité technique démontrée.

Ne remplace jamais spontanément :
- le rail par une sidebar large ;
- une table dense par des cards ;
- un panneau de détail par une page séparée ;
- des sparklines par des barres génériques ;
- les graphiques techniques par des graphiques simplifiés ;
- la densité desktop par de grands espacements.

Le **rail vertical compact** visible dans les maquettes est obligatoire.

## Workflow visuel obligatoire

Pour chaque écran :

1. ouvrir la maquette correspondante ;
2. relever sa structure et ses proportions ;
3. implémenter le layout Fyne ;
4. lancer HostDeck ;
5. produire une capture d’écran réelle ;
6. comparer visuellement la capture avec la maquette ;
7. corriger :
   - proportions ;
   - largeur du rail ;
   - hauteurs de lignes ;
   - densité ;
   - espacements ;
   - typographie ;
   - bordures ;
   - couleurs ;
   - sélection ;
   - tableaux ;
   - sparklines ;
   - graphiques ;
   - panneaux ;
8. répéter jusqu’à obtenir une correspondance visuelle forte.

Ne considère jamais un écran terminé simplement parce qu’il compile.

---

# 1. Vision produit

HostDeck est une application desktop native de monitoring d’infrastructure.

Elle doit permettre de :

- gérer plusieurs VPS / serveurs Linux ;
- superviser leur disponibilité ;
- se connecter en SSH directement ;
- utiliser un SSH Jump Host / Bastion ;
- tester les connexions ;
- collecter les métriques système ;
- surveiller Docker ;
- afficher les valeurs courantes ;
- conserver un historique local ;
- afficher des graphiques temporels professionnels ;
- afficher des sparklines directement dans les tables ;
- détecter et suivre les incidents ;
- gérer des règles d’alerte ;
- acquitter / résoudre des incidents ;
- envoyer des notifications desktop ;
- protéger correctement les credentials ;
- fonctionner localement sans backend SaaS obligatoire ;
- rester performante avec plusieurs dizaines, puis centaines de serveurs.

Quand HostDeck est ouvert, l’utilisateur doit immédiatement comprendre qu’il s’agit d’un **outil professionnel de supervision système**.

Cela ne doit jamais ressembler à :

- un dashboard SaaS ;
- une landing page ;
- une webapp ;
- un admin panel générique ;
- une grille de grosses cards ;
- une interface mobile agrandie ;
- une UI générée génériquement par IA.

---

# 2. Stack obligatoire

Utiliser :

## Backend / Core

- **Go stable**
- `context`
- goroutines contrôlées
- `errgroup`
- semaphores lorsque pertinent
- `golang.org/x/crypto/ssh`
- client Docker/Moby officiel ou API Docker Engine vérifiée via Context7
- SQLite
- `database/sql` avec driver SQLite choisi après analyse
- migrations versionnées
- UUID
- logging structuré
- erreurs typées / sentinelles adaptées
- stockage sécurisé des credentials par OS

## UI

- **Fyne 2.8+**
- Fyne `widget`
- Fyne `container`
- Fyne `canvas`
- custom widgets
- custom renderers
- custom layouts
- custom theme
- primitives GPU / canvas modernes de Fyne lorsque pertinentes

Ne pas utiliser :

- React
- Vue
- Angular
- Electron
- Tauri
- Wails
- HTML
- CSS
- JavaScript frontend
- WebView
- navigateur embarqué

HostDeck doit être une vraie application desktop native Go + Fyne.

---

# 3. Context7 obligatoire

**Context7 est obligatoire.**

Avant toute intégration non triviale, utiliser Context7 pour vérifier les APIs actuelles.

Vérifier notamment :

- Fyne ;
- Fyne Canvas ;
- custom widgets / renderers ;
- Fyne threading / `fyne.Do` ;
- `golang.org/x/crypto/ssh` ;
- Docker/Moby client ;
- SQLite driver choisi ;
- notifications desktop ;
- secure credential storage ;
- toute autre dépendance ajoutée.

Ne jamais se fier uniquement à la mémoire interne du modèle.

Ne jamais inventer une API.

Ordre de priorité documentaire :

1. Context7
2. documentation officielle
3. repository officiel / exemples
4. code source du package

Avant d’ajouter une dépendance :

1. vérifier sa version ;
2. vérifier sa maintenance ;
3. vérifier sa licence ;
4. vérifier sa compatibilité Windows/macOS/Linux ;
5. vérifier son impact sur le binaire ;
6. vérifier CGO éventuel ;
7. éviter les dépendances lourdes inutiles.

---

# 4. Langues

## Communication avec moi

Toujours me parler en **français**.

Cela inclut :

- plans ;
- analyses ;
- questions ;
- décisions ;
- comptes rendus ;
- reviews ;
- rapports finaux.

## Code

Tout le code et les identifiants techniques doivent être en **anglais** :

- fichiers ;
- dossiers ;
- packages ;
- structs ;
- interfaces ;
- fonctions ;
- variables ;
- DTO ;
- entities ;
- repositories ;
- use cases ;
- services ;
- tables SQLite ;
- colonnes SQLite ;
- migrations ;
- erreurs internes.

## Interface utilisateur

Tous les textes visibles par l’utilisateur doivent être en **français**.

## Commentaires

Les commentaires dans le code doivent être en **français**, uniquement lorsqu’ils apportent un vrai contexte.

## Documentation technique

La documentation du repository doit être en **anglais**.

---

# 5. Architecture globale

Le projet doit avoir une architecture modulaire, claire, testable et maintenable.

Structure recommandée :

```text
hostdeck/
├── cmd/
│   └── hostdeck/
│       └── main.go
│
├── internal/
│   ├── domain/
│   │   ├── server/
│   │   ├── monitoring/
│   │   ├── docker/
│   │   ├── incident/
│   │   └── shared/
│   │
│   ├── application/
│   │   ├── dto/
│   │   ├── ports/
│   │   ├── servers/
│   │   ├── monitoring/
│   │   ├── docker/
│   │   ├── incidents/
│   │   ├── alerts/
│   │   └── settings/
│   │
│   ├── infrastructure/
│   │   ├── ssh/
│   │   ├── docker/
│   │   ├── persistence/
│   │   ├── credentials/
│   │   ├── notifications/
│   │   └── logging/
│   │
│   ├── presentation/
│   │   └── fyne/
│   │       ├── app/
│   │       ├── navigation/
│   │       ├── screens/
│   │       ├── components/
│   │       ├── charts/
│   │       ├── theme/
│   │       ├── state/
│   │       └── dialogs/
│   │
│   └── bootstrap/
│
├── tests/
│   └── fixtures/
│
├── docs/
├── scripts/
├── .github/
│   └── workflows/
│
├── AGENTS.md
├── README.md
├── CONTRIBUTING.md
└── SECURITY.md
```

Éviter les packages fourre-tout.

Pas de `utils` global rempli de fonctions sans cohérence.

---

# 6. Règles de dépendance

Flux principal :

```text
Fyne UI
  ↓
Presentation Controller / Presenter
  ↓
Application Use Case
  ↓
Port / Interface
  ↓
Infrastructure
```

Le Domain ne dépend jamais de :

- Fyne ;
- SSH ;
- SQLite ;
- Docker client ;
- credential store ;
- notifications.

La Presentation ne doit pas accéder directement à :

- SQL ;
- Docker API ;
- `x/crypto/ssh` ;
- keyring ;
- filesystem métier.

---

# 7. Domain

Créer notamment :

```text
Server
ServerID
ServerName
Host
Port
ServerGroup
ServerTag
ConnectionMode
ServerStatus

MetricSample
CPUUsage
MemoryUsage
SwapUsage
DiskUsage
NetworkUsage
LoadAverage
Uptime

DockerHost
DockerContainer
DockerContainerID
DockerContainerStatus
DockerContainerStats
DockerImage
DockerVolume
DockerNetwork

Severity
Incident
IncidentID
IncidentStatus
AlertRule
AlertStatus
Threshold
MonitoringInterval
RetentionPolicy
```

Éviter les strings arbitraires pour les états métier.

Exemple :

```go
type ServerStatus string

const (
    ServerStatusOnline               ServerStatus = "online"
    ServerStatusOffline              ServerStatus = "offline"
    ServerStatusGatewayUnavailable   ServerStatus = "gateway_unavailable"
    ServerStatusAuthenticationFailed ServerStatus = "authentication_failed"
    ServerStatusUnknown              ServerStatus = "unknown"
)
```

---

# 8. DTO obligatoires

Les DTO sont séparés du Domain.

Créer notamment :

```text
CreateServerDTO
UpdateServerDTO
ServerSummaryDTO
ServerDetailsDTO
ServerConfigurationDTO
TestConnectionRequestDTO
TestConnectionResultDTO

LatestMetricDTO
MetricHistoryRequestDTO
MetricHistoryDTO
MetricStatisticsDTO

DockerContainerSummaryDTO
DockerContainerDetailsDTO
DockerContainerStatsDTO
DockerHostInfoDTO

IncidentDTO
IncidentDetailsDTO
AcknowledgeIncidentDTO

AlertRuleDTO
SettingsDTO
```

Les conversions doivent être explicites et testées.

---

# 9. Use Cases

Serveurs :

```text
AddServerUseCase
UpdateServerUseCase
DeleteServerUseCase
GetServersUseCase
GetServerDetailsUseCase
TestServerConnectionUseCase
```

Monitoring :

```text
CollectServerMetricsUseCase
GetLatestMetricsUseCase
GetMetricHistoryUseCase
StartFleetMonitoringUseCase
StopFleetMonitoringUseCase
```

Docker :

```text
GetDockerInfoUseCase
ListDockerContainersUseCase
GetDockerContainerDetailsUseCase
GetDockerContainerStatsUseCase
StartDockerContainerUseCase
StopDockerContainerUseCase
RestartDockerContainerUseCase
GetDockerContainerLogsUseCase
```

Incidents :

```text
GetIncidentsUseCase
GetIncidentDetailsUseCase
AcknowledgeIncidentUseCase
ResolveIncidentUseCase
```

Alerts :

```text
CreateAlertRuleUseCase
UpdateAlertRuleUseCase
DeleteAlertRuleUseCase
```

Settings :

```text
GetSettingsUseCase
UpdateSettingsUseCase
```

Chaque Use Case doit avoir une seule responsabilité.

---

# 10. Interfaces / Ports

Créer uniquement les interfaces utiles.

Exemple :

```go
type ServerRepository interface {}
type MetricsRepository interface {}
type IncidentRepository interface {}
type AlertRuleRepository interface {}
type SettingsRepository interface {}

type CredentialStore interface {}

type SSHConnection interface {
    Execute(ctx context.Context, command string) (CommandResult, error)
    Close() error
}

type SSHConnectionFactory interface {
    Connect(ctx context.Context, server domain.Server) (SSHConnection, error)
}

type ContainerRuntime interface {
    ListContainers(ctx context.Context, serverID ServerID) ([]Container, error)
    GetStats(ctx context.Context, containerID ContainerID) (ContainerStats, error)
    GetLogs(ctx context.Context, containerID ContainerID) ([]LogEntry, error)
}

type DesktopNotificationService interface {}
```

Ne pas créer des interfaces inutiles juste “pour faire clean architecture”.

---

# 11. SSH direct

Utiliser `golang.org/x/crypto/ssh`.

Supporter :

- host ;
- port ;
- username ;
- SSH key ;
- passphrase ;
- host key verification ;
- timeout ;
- keep-alive ;
- reconnexion ;
- context cancellation ;
- erreurs typées ;
- fermeture propre.

Ne jamais désactiver la vérification des host keys simplement pour simplifier.

---

# 12. SSH Jump Host / Bastion

Supporter :

```text
HostDeck
   ↓ SSH
Bastion
   ↓ tunneled connection
Target VPS
```

Le monitoring ne doit pas connaître le mode réel de connexion.

API conceptuelle :

```go
conn, err := sshConnectionFactory.Connect(ctx, server)
```

Prévoir :

- plusieurs VPS partageant un bastion ;
- connection reuse ;
- pooling raisonnable ;
- limite de connexions/channels ;
- timeout ;
- backoff exponentiel ;
- keep-alive ;
- invalidation ;
- shutdown.

Si le bastion tombe :

```text
Bastion         Offline
Targets         GatewayUnavailable
```

Ne pas générer un incident offline par cible si la racine est le bastion.

---

# 13. Docker

Prévoir Docker dès l’architecture V1.

Utiliser le client Docker/Moby officiel ou l’API Docker Engine via une abstraction vérifiée avec Context7.

Ne pas parser `docker ps` partout.

Créer un adapter derrière :

```go
type ContainerRuntime interface {}
```

Supporter :

- Docker info ;
- containers ;
- status ;
- CPU ;
- RAM ;
- network RX/TX ;
- block I/O ;
- restart count ;
- logs ;
- start ;
- stop ;
- restart ;
- images ;
- volumes ;
- networks.

Pour un Docker distant :

- privilégier l’API Docker Engine de manière sécurisée ;
- ou une connexion via SSH adaptée ;
- ne jamais exposer le socket Docker publiquement sans protection.

Préparer l’architecture pour Podman plus tard.

---

# 14. Credentials

Créer `CredentialStore`.

Utiliser :

- macOS Keychain ;
- Windows Credential Manager ;
- Linux Secret Service / keyring.

Ne jamais stocker en clair :

- private key ;
- password ;
- passphrase ;
- API secret.

SQLite ne conserve qu’un identifiant ou une référence.

Ne jamais logger de secret.

---

# 15. Monitoring Linux

Collecter au minimum :

- CPU ;
- RAM ;
- swap ;
- disk ;
- load average 1/5/15 ;
- uptime ;
- network RX/TX ;
- hostname ;
- OS/distribution ;
- kernel.

Sources privilégiées :

```text
/proc/stat
/proc/meminfo
/proc/loadavg
/proc/net/dev
/proc/uptime
/etc/os-release
df
uname
```

---

# 16. Parsers séparés

Flux :

```text
SSH Execute
 ↓
Raw output
 ↓
Parser
 ↓
Domain metric
 ↓
Repository
```

Créer :

```text
CPUStatParser
MemInfoParser
LoadAverageParser
NetworkDevParser
DiskUsageParser
UptimeParser
OSReleaseParser
```

Les parsers doivent être purs et testables.

---

# 17. Fleet Monitoring Coordinator

Créer un composant dédié :

```text
FleetMonitoringCoordinator
├── scheduling
├── concurrency limit
├── SSH reuse
├── Docker polling
├── cancellation
├── retry
├── timeout
├── backoff
├── persistence
├── incident evaluation
└── event publication
```

Utiliser :

- `context.Context`
- `errgroup`
- semaphores
- tickers contrôlés

Ne pas créer des goroutines orphelines.

Chaque goroutine doit avoir :

- owner ;
- cancellation ;
- arrêt propre.

---

# 18. SQLite

Choisir le driver après analyse.

Critères :

- cross-platform ;
- CGO ;
- packaging Fyne ;
- performance ;
- maintenance ;
- tests.

Tables minimales :

```text
servers
server_tags
server_groups
metric_samples
disk_metric_samples
network_metric_samples

docker_hosts
docker_containers
docker_metric_samples

incidents
incident_events
alert_rules
settings
ssh_host_keys
```

Prévoir :

- migrations ;
- foreign keys ;
- indexes ;
- transactions ;
- pagination ;
- retention ;
- pruning ;
- agrégation.

Les records persistence sont distincts des entities Domain.

---

# 19. Historique / downsampling

Plages :

```text
15 min
1 h
2 h
6 h
12 h
24 h
7 j
30 j
```

Le repository renvoie des datasets adaptés.

Ne jamais charger des centaines de milliers de points inutilement.

Conserver lorsque pertinent :

- current ;
- min ;
- average ;
- max.

---

# 20. Incidents

Créer un moteur réel d’incidents.

Sévérités :

```text
Information
Warning
Average
High
Critical
```

Un Incident contient :

- id ;
- server ;
- metric ;
- severity ;
- rule ;
- started_at ;
- last_updated_at ;
- acknowledged_at ;
- recovered_at ;
- status ;
- current_value ;
- threshold.

États :

```text
Open
Acknowledged
Recovered
Resolved
```

---

# 21. Règles d’alerte

Supporter :

- server unavailable ;
- gateway unavailable ;
- CPU ;
- RAM ;
- disk ;
- load ;
- SSH latency ;
- Docker container stopped ;
- Docker container unhealthy ;
- Docker restart count élevé.

Une règle contient :

```text
metric
operator
threshold
duration
severity
cooldown
scope
enabled
```

---

# 22. Architecture Presentation / Fyne

Structure recommandée :

```text
presentation/fyne/
├── app/
├── navigation/
├── screens/
│   ├── infrastructure/
│   ├── incidents/
│   ├── live_data/
│   ├── docker/
│   ├── alerts/
│   ├── host_configuration/
│   └── settings/
├── components/
├── charts/
├── theme/
├── state/
├── controllers/
└── dialogs/
```

Ne pas mettre toute l’UI dans `main.go`.

Chaque écran doit avoir son propre état de présentation.

---

# 23. Maquettes = source de vérité visuelle

Les maquettes validées sont obligatoires comme référence.

Reproduire :

- rail vertical ;
- topbar compacte ;
- navigation secondaire ;
- tables denses ;
- panel détail ;
- status bar ;
- couleurs de severity ;
- sparklines ;
- charts techniques ;
- faible padding ;
- dark theme premium ;
- lignes et bordures fines ;
- sélection discrète ;
- interactions desktop.

Le résultat doit être visiblement issu de ces maquettes.

---

# 24. Rail vertical

Pas de grosse sidebar webapp.

Utiliser un **rail vertical compact**.

Exemple :

```text
┌──────┐
│  HD  │
├──────┤
│  ▦   │ Infrastructure
│  ⚠   │ Incidents
│  ≋   │ Live Data
│  ◫   │ Docker
│  🔔  │ Alerts
│      │
│  ⚙   │ Settings
└──────┘
```

Caractéristiques :

- width réduite ;
- icônes centrées ;
- état actif visible ;
- tooltip ;
- pas de labels permanents ;
- sections séparées ;
- compteur incidents possible ;
- compact.

---

# 25. Layout principal

```text
┌──────┬──────────────────────────────────────────────────┐
│ Rail │ Topbar                                           │
│      ├──────────────────────────────────────────────────┤
│      │ Secondary nav │ Main content                    │
│      │               │                                 │
│      │               │                                 │
│      │               │                                 │
├──────┴───────────────┴─────────────────────────────────┤
│ Status bar                                             │
└────────────────────────────────────────────────────────┘
```

Panneau détail :

```text
Main Table
+
Resizable Details Panel
```

Le panneau ne doit pas être ouvert s’il n’y a aucune sélection.

---

# 26. Infrastructure / Hosts

Table dense :

```text
État | Hôte | IP | CPU | RAM | Disque | RX/TX | Load | Uptime | Collecte
```

Chaque ligne peut contenir :

- status dot ;
- hostname ;
- tags ;
- métrique ;
- sparkline ;
- last check.

Supporter :

- sélection ;
- tri ;
- recherche ;
- filtres ;
- groupes ;
- tags ;
- contexte menu ;
- navigation clavier ;
- scroll performant.

Fyne `widget.Table` doit être utilisé ou étendu correctement pour profiter de la virtualisation.

---

# 27. Incidents

Table :

```text
Severity
Time
Status
Host
Problem
Duration
Acknowledged
Last update
```

Panneau détail :

- chronologie ;
- métrique ;
- seuil ;
- chart ;
- acknowledge ;
- resolve ;
- notes ;
- audit.

---

# 28. Live Data

Créer une vue :

```text
Metric
Latest
Updated
Min
Avg
Max
History
```

Avec filtres :

- host ;
- catégorie ;
- search.

---

# 29. Docker

Créer une vue Docker dédiée.

Table :

```text
État | Container | Image | CPU | RAM | RX/TX | Restart | Uptime
```

Détail container :

- status ;
- image ;
- command ;
- ports ;
- networks ;
- volumes ;
- CPU chart ;
- RAM chart ;
- network chart ;
- logs ;
- start/stop/restart.

Ne pas créer des grosses cards.

---

# 30. Configuration Host

Table à gauche + détail à droite.

Sections :

```text
Identity
Tags
SSH
Authentication
Jump Host
Monitoring
Docker
Delete Host
```

Supporter :

- direct ;
- jump host ;
- SSH key ;
- passphrase ;
- monitoring interval ;
- Docker enabled ;
- test connection ;
- fingerprint ;
- delete confirmation.

---

# 31. Graphiques type Netdata

Les charts sont une exigence majeure.

Le style doit être proche du ressenti Netdata :

- très technique ;
- dense ;
- dark ;
- grille fine ;
- lignes fines ;
- couleurs nettes ;
- plusieurs séries ;
- stacked areas si pertinent ;
- peu de décoration ;
- labels petits ;
- données prioritaires.

Créer :

```text
TimeSeriesChart
StackedAreaChart
Sparkline
ThresholdOverlay
Crosshair
Tooltip
AxisRenderer
GridRenderer
Legend
TimeRangeSelector
```

---

# 32. Fyne Canvas — moteur de graphiques custom

Ne pas chercher à forcer un composant de chart tiers si cela limite le design.

Préférer un moteur léger custom basé sur Fyne Canvas.

Créer une architecture :

```text
charts/
├── model.go
├── renderer.go
├── axes.go
├── grid.go
├── line.go
├── area.go
├── stacked_area.go
├── sparkline.go
├── tooltip.go
├── crosshair.go
├── threshold.go
└── scale.go
```

Utiliser les primitives GPU / canvas modernes de Fyne 2.8 lorsque approprié.

Vérifier avec Context7 :

- CanvasObject ;
- custom renderer ;
- refresh ;
- GPU shapes ;
- shader ;
- Bézier / polyline ;
- clipping ;
- HiDPI.

---

# 33. Performance des charts

Ne pas recréer tous les objets Canvas à chaque frame.

Prévoir :

- pooling ;
- cache ;
- reuse ;
- dataset borné ;
- downsampling ;
- invalidation ciblée.

Pour les sparklines de table :

- renderer ultra léger ;
- pas de labels ;
- pas d’axes ;
- dataset court ;
- pas de tooltip dans chaque cellule si trop coûteux.

---

# 34. Design System Fyne

Créer un thème HostDeck centralisé.

Structure :

```text
theme/
├── colors.go
├── typography.go
├── spacing.go
├── sizes.go
├── severity.go
├── icons.go
└── theme.go
```

Tokens :

```text
surfaceBackground
surfaceRail
surfacePanel
surfaceRaised

borderSubtle

textPrimary
textSecondary
textMuted

accent
selection

statusOnline
statusOffline
statusWarning

severityInformation
severityWarning
severityAverage
severityHigh
severityCritical
```

Pas de couleurs dispersées dans les screens.

---

# 35. Custom Widgets

Créer uniquement lorsque nécessaire :

```text
RailButton
StatusIndicator
SeverityBadge
MetricValue
MetricSparkline
MonitoringTableCell
TimeSeriesChart
FilterBar
SearchField
PanelHeader
HostRow
IncidentRow
TagBadge
EmptyState
LoadingState
ErrorState
StatusBar
ResizablePanel
```

Utiliser :

- `fyne.Widget`
- `fyne.WidgetRenderer`
- `CanvasObject`

proprement.

---

# 36. Fyne threading

Fyne impose un thread UI principal.

Respecter la documentation actuelle.

Lorsque le backend veut modifier l’UI :

- utiliser `fyne.Do(...)` ou API actuelle équivalente ;
- ne jamais modifier les widgets depuis une goroutine arbitraire ;
- ne jamais bloquer le thread UI.

Les opérations SSH/Docker/SQLite longues restent dans le backend.

---

# 37. Performance générale

Objectifs :

- scroll fluide ;
- CPU faible au repos ;
- update ciblée ;
- tables capables de centaines de lignes ;
- graphs fluides ;
- pas de refresh global continu.

Éviter :

- `Refresh()` sur toute la fenêtre ;
- gros recalculs chaque seconde ;
- allocations massives ;
- duplication des métriques ;
- historiques non bornés.

---

# 38. Erreurs

Utiliser :

- erreurs sentinelles ;
- types d’erreurs ;
- wrapping `%w` ;
- `errors.Is` ;
- `errors.As`.

Créer notamment :

```text
ValidationError
RepositoryError
SSHError
DockerError
MonitoringError
CredentialError
NotificationError
```

Les erreurs UI sont traduites en français dans Presentation.

---

# 39. Logging

Logging structuré.

Catégories :

```text
application
ssh
docker
monitoring
database
incidents
ui
```

Inclure lorsque pertinent :

- server_id ;
- container_id ;
- operation ;
- duration ;
- result.

Aucun secret dans les logs.

---

# 40. Tests Domain

Tester :

- validations ;
- statuses ;
- thresholds ;
- severities ;
- incidents ;
- connection mode ;
- cooldown ;
- retention.

---

# 41. Tests Application

Mocker les interfaces.

Tester :

- CRUD server ;
- invalid server ;
- jump host missing ;
- jump host cycle ;
- SSH timeout ;
- auth failure ;
- host key failure ;
- gateway unavailable ;
- repository failure ;
- incident creation ;
- acknowledge ;
- resolve ;
- Docker container states ;
- alert cooldown.

---

# 42. Tests parsers Linux

Fixtures :

```text
Ubuntu 22.04
Ubuntu 24.04
Debian 12
Debian 13
Fedora
Rocky Linux
AlmaLinux
```

Tester :

- valeurs normales ;
- valeurs invalides ;
- valeurs manquantes ;
- plusieurs interfaces ;
- plusieurs partitions.

---

# 43. Tests SQLite

Base réelle temporaire.

Tester :

- migrations ;
- insert ;
- update ;
- delete ;
- transactions ;
- indexes ;
- queries temporelles ;
- retention ;
- foreign keys.

---

# 44. Tests SSH

Unit tests :

- fake `SSHConnection` ;
- fake factory ;
- timeout ;
- context cancellation ;
- command errors.

Integration :

```text
Client
 ↓
SSH Bastion
 ↓
SSH Target
```

Utiliser containers si pertinent.

---

# 45. Tests Docker

Unit tests :

- fake `ContainerRuntime` ;
- containers ;
- stats ;
- logs ;
- restart ;
- errors.

Integration tests séparés avec Docker Engine réel dans CI dédiée si raisonnable.

---

# 46. Tests UI Fyne

Tester les comportements importants sans dépendre du rendu pixel-perfect.

Tester :

- navigation rail ;
- sélection host ;
- opening details ;
- closing details ;
- filters ;
- incidents ;
- dialog confirmation ;
- loading ;
- error ;
- empty states.

Utiliser les APIs de test Fyne appropriées.

---

# 47. Qualité de code stricte

Exigence niveau production.

Interdit :

- énorme `main.go` ;
- God Services ;
- globals métier ;
- goroutines sans lifecycle ;
- panics non justifiés ;
- erreurs ignorées ;
- `_ = err` arbitraires ;
- context.Background() partout sans ownership ;
- magic numbers ;
- SQL dans Presentation ;
- SSH dans Presentation ;
- Docker dans Presentation ;
- fichiers de milliers de lignes ;
- duplication ;
- interfaces inutiles ;
- packages `utils` fourre-tout.

Préférer :

- petites APIs ;
- responsabilités nettes ;
- structs focalisées ;
- composition ;
- injection explicite ;
- context propagation ;
- fonctions courtes ;
- erreurs wrappées ;
- ressources fermées avec `defer`.

---

# 48. Analyse statique

Avant une tâche terminée :

```bash
go fmt ./...
go vet ./...
go test ./...
```

Ajouter :

```bash
staticcheck ./...
```

si installé/configuré.

Aucun warning sérieux ne doit rester sans justification.

---

# 49. Race detector

Utiliser régulièrement :

```bash
go test -race ./...
```

notamment sur :

- monitoring coordinator ;
- connection pools ;
- Docker stats ;
- caches ;
- event bus.

Toute race doit être corrigée.

---

# 50. Mémoire / leaks

Vérifier :

- goroutines qui augmentent sans limite ;
- tickers non stoppés ;
- channels non fermés ;
- connections SSH non fermées ;
- Docker streams non fermés ;
- SQLite rows non fermées ;
- caches sans limite ;
- datasets chart non bornés.

Utiliser `pprof` si nécessaire.

---

# 51. Sécurité

Exigences :

- secrets hors SQLite ;
- secrets hors logs ;
- host key verification ;
- validation des entrées ;
- commandes SSH internes contrôlées ;
- pas d’injection shell ;
- pas de socket Docker exposé publiquement ;
- credential storage natif ;
- accès Docker sécurisé.

---

# 52. CI

Créer GitHub Actions :

```bash
go fmt
go vet ./...
go test ./...
go test -race ./...
staticcheck ./...
```

Build au minimum sur :

- Linux ;
- Windows ;
- macOS ;

si Fyne et les runners le permettent.

---

# 53. Documentation

Créer :

```text
docs/
├── ARCHITECTURE.md
├── DATABASE.md
├── SSH.md
├── DOCKER.md
├── MONITORING.md
├── INCIDENTS.md
├── SECURITY.md
├── TESTING.md
├── UI_DESIGN.md
└── PERFORMANCE.md
```

Créer également :

- README.md
- AGENTS.md
- CONTRIBUTING.md
- SECURITY.md

---

# 54. AGENTS.md

Inclure au minimum :

1. communication en français ;
2. code en anglais ;
3. UI en français ;
4. commentaires en français ;
5. docs techniques en anglais ;
6. Context7 obligatoire ;
7. Fyne uniquement pour UI ;
8. Domain indépendant de Fyne ;
9. DTO séparés ;
10. models persistence séparés ;
11. pas de SQL/SSH/Docker dans Presentation ;
12. goroutines contrôlées ;
13. race detector régulier ;
14. secrets hors SQLite ;
15. tests obligatoires ;
16. staticcheck ;
17. maquettes = référence visuelle ;
18. rail vertical obligatoire ;
19. charts type Netdata ;
20. inspection visuelle obligatoire ;
21. pas de refactor massif sans justification ;
22. pas de dépendance ajoutée sans vérification.

---

# 55. Écrans V1

Créer au minimum :

## Infrastructure
- rail ;
- groupes/environnements ;
- hosts table ;
- metrics ;
- sparklines ;
- details panel.

## Incidents
- filters ;
- incident table ;
- severity ;
- timeline ;
- context chart ;
- acknowledge ;
- resolve.

## Live Data
- latest metrics ;
- min/avg/max ;
- history.

## Docker
- containers ;
- images ;
- stats ;
- logs ;
- actions.

## Alertes
- rules ;
- status ;
- history.

## Configuration Host
- identity ;
- tags ;
- SSH ;
- jump host ;
- credentials ;
- Docker ;
- polling ;
- test connection.

## Paramètres
- polling ;
- retention ;
- notifications ;
- appearance ;
- logs.

---

# 56. V2 à préparer

Préparer :

- systemd ;
- journald ;
- process list ;
- ports ;
- HTTP/HTTPS ;
- TLS expiration ;
- terminal SSH ;
- topology ;
- autodiscovery ;
- custom dashboards ;
- Podman ;
- HostDeck agent.

Ne pas implémenter prématurément.

---

# 57. Workflow agent

Avant de coder :

1. inspecter le repo ;
2. lire ce prompt ;
3. inspecter toutes les maquettes ;
4. utiliser Context7 ;
5. vérifier Fyne ;
6. définir architecture ;
7. définir Domain ;
8. définir DTO ;
9. définir interfaces ;
10. définir SQLite ;
11. définir SSH/jump host ;
12. définir Docker ;
13. définir monitoring coordinator ;
14. définir Presentation Fyne ;
15. définir charts Canvas ;
16. définir Design System ;
17. définir tests ;
18. me présenter le plan en français.

Implémenter ensuite par étapes cohérentes.

---

# 58. Validation UI

Pour chaque écran :

1. build ;
2. lancer HostDeck ;
3. capture écran ;
4. comparer aux maquettes ;
5. corriger proportions ;
6. corriger rail ;
7. corriger typography ;
8. corriger spacing ;
9. corriger tables ;
10. corriger charts ;
11. vérifier dark mode ;
12. vérifier resize ;
13. vérifier HiDPI ;
14. vérifier loading/empty/error.

La validation visuelle est obligatoire.

---

# 59. Critères de validation finale

La V1 n’est terminée que si :

- build OK ;
- tests OK ;
- race detector OK ;
- staticcheck OK ;
- architecture respectée ;
- UI Fyne uniquement ;
- rail vertical fidèle ;
- SSH direct OK ;
- Jump Host OK ;
- host key verification OK ;
- Docker OK ;
- CRUD hosts OK ;
- monitoring OK ;
- persistence OK ;
- downsampling OK ;
- sparklines OK ;
- charts Netdata-like OK ;
- incidents OK ;
- credentials sécurisés ;
- pas de secrets loggés ;
- pas de goroutine leak connue ;
- pas de croissance mémoire anormale ;
- UI comparable aux maquettes ;
- documentation à jour.

---

# 60. Résultat attendu

HostDeck doit donner l’impression d’un logiciel de supervision mature.

Priorités :

1. qualité de code ;
2. architecture ;
3. fiabilité ;
4. sécurité ;
5. tests ;
6. monitoring ;
7. Docker ;
8. performance ;
9. qualité visuelle ;
10. maintenabilité.

Le résultat final doit être une interprétation native **Go + Fyne** des maquettes validées.

Les maquettes sont la référence visuelle principale.

Le backend doit rester propre, testable, concurrent et sécurisé.

L’UI doit être dense, technique, premium et ressembler à un vrai outil de monitoring, avec des graphiques proches du style Netdata et un rail vertical compact.
