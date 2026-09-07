# HostDeck — Prompt complet de création en C# + Avalonia

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

1. une convention Avalonia par défaut ;
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
3. implémenter le layout Avalonia/XAML ;
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

HostDeck est une application desktop de monitoring d’infrastructure.

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

## Runtime / langage

Utiliser :

- **C# moderne** ;
- **.NET 10 LTS** ;
- nullable reference types activés ;
- implicit usings uniquement si cela reste lisible ;
- `async` / `await` ;
- `CancellationToken` propagé correctement ;
- `Task` / `ValueTask` lorsque pertinent ;
- `PeriodicTimer` pour les boucles périodiques ;
- `SemaphoreSlim` pour limiter la concurrence ;
- `System.Threading.Channels` lorsque pertinent ;
- `IAsyncEnumerable<T>` lorsque le streaming est réellement utile ;
- `Microsoft.Extensions.Hosting` pour le lifecycle de l’application et des services de fond ;
- `Microsoft.Extensions.DependencyInjection` ;
- `Microsoft.Extensions.Logging` ;
- `Microsoft.Extensions.Options` uniquement lorsque pertinent.

## UI

Utiliser :

- **Avalonia 12 stable ou version stable plus récente compatible avec .NET 10, vérifiée via Context7** ;
- Avalonia XAML ;
- MVVM ;
- `CommunityToolkit.Mvvm` ou une alternative légère uniquement après vérification ;
- `DataGrid` Avalonia pour les tables denses lorsqu’il répond au besoin ;
- `Grid`, `DockPanel`, `ItemsControl`, `ScrollViewer`, `GridSplitter` et contrôles Avalonia standards ;
- `TemplatedControl` pour les composants réutilisables fortement stylés ;
- contrôles custom dessinés lorsque nécessaire ;
- `DrawingContext` pour les graphiques custom ;
- `CompositionCustomVisualHandler` / composition Avalonia uniquement lorsque justifié par les besoins de rendu temps réel et après vérification de l’API actuelle.

## Infrastructure

Prévoir :

- **SSH.NET** pour SSH/SFTP/port forwarding, version stable actuelle vérifiée via Context7/NuGet ;
- **Docker.DotNet** ou l’API Docker Engine via un client .NET officiellement maintenu et vérifié ;
- **Microsoft.Data.Sqlite** pour SQLite ;
- migrations versionnées gérées par HostDeck ;
- UUID via `Guid` ;
- stockage sécurisé des credentials par OS ;
- notifications desktop via abstraction cross-platform avec adapters OS spécifiques.

## Interdictions

Ne pas utiliser :

- React ;
- Vue ;
- Angular ;
- Electron ;
- Tauri ;
- Wails ;
- Blazor Hybrid ;
- HTML/CSS pour construire l’interface ;
- JavaScript frontend ;
- WebView ;
- navigateur embarqué.

HostDeck doit être une vraie application desktop **C#/.NET + Avalonia**, sans couche web.

---

# 3. Context7 obligatoire

**Context7 est obligatoire.**

Avant toute intégration non triviale, utiliser Context7 pour vérifier les APIs actuelles.

Vérifier notamment :

- .NET 10 ;
- Avalonia 12+ ;
- Avalonia XAML ;
- Avalonia `DataGrid` ;
- custom controls / `TemplatedControl` ;
- `DrawingContext` ;
- composition / `CompositionCustomVisualHandler` ;
- threading Avalonia / Dispatcher ;
- `CommunityToolkit.Mvvm` si utilisé ;
- `Microsoft.Extensions.Hosting` ;
- SSH.NET ;
- Docker.DotNet ou client Docker retenu ;
- Microsoft.Data.Sqlite ;
- notifications desktop ;
- secure credential storage ;
- packaging Windows/macOS/Linux ;
- toute autre dépendance ajoutée.

Ne jamais se fier uniquement à la mémoire interne du modèle.

Ne jamais inventer une API.

Ordre de priorité documentaire :

1. Context7 ;
2. documentation officielle ;
3. repository officiel / exemples ;
4. code source du package.

Avant d’ajouter une dépendance NuGet :

1. vérifier sa dernière version stable ;
2. vérifier sa maintenance ;
3. vérifier sa licence ;
4. vérifier sa compatibilité .NET 10 ;
5. vérifier sa compatibilité Windows/macOS/Linux ;
6. vérifier son impact sur le binaire ;
7. vérifier les dépendances natives éventuelles ;
8. vérifier la compatibilité trimming/AOT si cela devient un objectif ;
9. éviter les dépendances lourdes inutiles.

Ne fige pas arbitrairement une version obsolète si une version stable plus récente est disponible et compatible.

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
- namespaces ;
- classes ;
- structs ;
- records ;
- interfaces ;
- enums ;
- propriétés ;
- méthodes ;
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
├── src/
│   ├── HostDeck.Domain/
│   │   ├── Servers/
│   │   ├── Monitoring/
│   │   ├── Docker/
│   │   ├── Incidents/
│   │   ├── Alerts/
│   │   └── Shared/
│   │
│   ├── HostDeck.Application/
│   │   ├── DTOs/
│   │   ├── Ports/
│   │   ├── Servers/
│   │   ├── Monitoring/
│   │   ├── Docker/
│   │   ├── Incidents/
│   │   ├── Alerts/
│   │   └── Settings/
│   │
│   ├── HostDeck.Infrastructure/
│   │   ├── SSH/
│   │   ├── Docker/
│   │   ├── Persistence/
│   │   │   ├── Sqlite/
│   │   │   ├── Migrations/
│   │   │   └── Records/
│   │   ├── Credentials/
│   │   ├── Notifications/
│   │   └── Logging/
│   │
│   ├── HostDeck.Presentation/
│   │   ├── ViewModels/
│   │   ├── Views/
│   │   ├── Controls/
│   │   ├── Charts/
│   │   ├── Themes/
│   │   ├── Navigation/
│   │   ├── Dialogs/
│   │   ├── Converters/
│   │   └── State/
│   │
│   └── HostDeck.Desktop/
│       ├── App.axaml
│       ├── App.axaml.cs
│       ├── Program.cs
│       ├── Bootstrap/
│       └── Assets/
│
├── tests/
│   ├── HostDeck.Domain.Tests/
│   ├── HostDeck.Application.Tests/
│   ├── HostDeck.Infrastructure.Tests/
│   ├── HostDeck.Presentation.Tests/
│   ├── HostDeck.IntegrationTests/
│   └── Fixtures/
│
├── docs/
├── scripts/
├── .github/
│   └── workflows/
│
├── Directory.Build.props
├── Directory.Packages.props
├── HostDeck.sln
├── AGENTS.md
├── README.md
├── CONTRIBUTING.md
└── SECURITY.md
```

Utiliser les **Central Package Versions** via `Directory.Packages.props` si cela reste compatible avec les outils retenus.

Éviter les projets ou namespaces fourre-tout.

Pas de `Helpers` ou `Utils` global rempli de fonctions sans cohérence.

---

# 6. Règles de dépendance

Flux principal :

```text
Avalonia View
  ↓ Binding / Command
ViewModel
  ↓
Application Use Case / Service
  ↓
Port / Interface
  ↓
Infrastructure Adapter
```

Le Domain ne dépend jamais de :

- Avalonia ;
- SSH.NET ;
- Microsoft.Data.Sqlite ;
- Docker.DotNet ;
- credential store ;
- notifications ;
- `Microsoft.Extensions.Hosting` si cela pollue le modèle métier.

La Presentation ne doit pas accéder directement à :

- SQL ;
- Docker API ;
- SSH.NET ;
- credential store natif ;
- filesystem métier.

`HostDeck.Desktop` est le **composition root** :

- DI ;
- configuration ;
- logging ;
- lifecycle ;
- création de la fenêtre principale ;
- démarrage/arrêt des services de fond.

---

# 7. Domain

Créer notamment :

```text
Server
ServerId
ServerName
HostAddress
Port
ServerGroup
ServerTag
ConnectionMode
ServerStatus

MetricSample
CpuUsage
MemoryUsage
SwapUsage
DiskUsage
NetworkUsage
LoadAverage
Uptime

DockerHost
DockerContainer
DockerContainerId
DockerContainerStatus
DockerContainerStats
DockerImage
DockerVolume
DockerNetwork

Severity
Incident
IncidentId
IncidentStatus
AlertRule
AlertStatus
Threshold
MonitoringInterval
RetentionPolicy
```

Utiliser lorsque pertinent :

- `sealed record` ;
- `readonly record struct` pour les value objects légers ;
- enums pour les états fermés ;
- types dédiés lorsque cela renforce réellement les invariants.

Éviter les strings arbitraires pour les états métier.

Exemple :

```csharp
public enum ServerStatus
{
    Unknown,
    Online,
    Offline,
    GatewayUnavailable,
    AuthenticationFailed,
    HostKeyRejected
}
```

Les invariants importants doivent être protégés par le Domain et testés.

---

# 8. DTO obligatoires

Les DTO sont séparés du Domain.

Créer notamment :

```text
CreateServerDto
UpdateServerDto
ServerSummaryDto
ServerDetailsDto
ServerConfigurationDto
TestConnectionRequestDto
TestConnectionResultDto

LatestMetricDto
MetricHistoryRequestDto
MetricHistoryDto
MetricStatisticsDto

DockerContainerSummaryDto
DockerContainerDetailsDto
DockerContainerStatsDto
DockerHostInfoDto

IncidentDto
IncidentDetailsDto
AcknowledgeIncidentDto

AlertRuleDto
SettingsDto
```

Préférer des `record` immutables pour les DTO lorsque pertinent.

Les conversions Domain ↔ DTO doivent être explicites et testées.

Ne pas exposer directement les records SQLite ou les objets du SDK Docker/SSH à la Presentation.

---

# 9. Use Cases / Application Services

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

Les opérations I/O doivent accepter un `CancellationToken`.

Éviter les classes `Service` géantes contenant des dizaines de responsabilités.

---

# 10. Interfaces / Ports

Créer uniquement les interfaces utiles.

Exemple :

```csharp
public interface IServerRepository;
public interface IMetricsRepository;
public interface IIncidentRepository;
public interface IAlertRuleRepository;
public interface ISettingsRepository;

public interface ICredentialStore;

public interface ISshConnection : IAsyncDisposable
{
    Task<CommandResult> ExecuteAsync(
        string command,
        CancellationToken cancellationToken = default);
}

public interface ISshConnectionFactory
{
    Task<ISshConnection> ConnectAsync(
        Server server,
        CancellationToken cancellationToken = default);
}

public interface IContainerRuntime
{
    Task<IReadOnlyList<DockerContainer>> ListContainersAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default);

    Task<DockerContainerStats> GetStatsAsync(
        DockerContainerId containerId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<LogEntry> StreamLogsAsync(
        DockerContainerId containerId,
        CancellationToken cancellationToken = default);
}

public interface IDesktopNotificationService;
```

Ne pas créer des interfaces inutiles uniquement “pour faire Clean Architecture”.

Les interfaces appartiennent à la couche qui en a besoin, généralement Application.

---

# 11. SSH direct

Utiliser **SSH.NET** après vérification de l’API stable actuelle via Context7/NuGet.

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
- cancellation ;
- erreurs typées ;
- fermeture propre ;
- exécution de commandes ;
- port forwarding si nécessaire.

Exigences :

- ne jamais désactiver la vérification des host keys pour simplifier ;
- conserver les fingerprints approuvés localement ;
- distinguer première connexion, host key connue et host key modifiée ;
- une host key modifiée doit être traitée comme un événement de sécurité ;
- limiter les timeouts ;
- gérer les exceptions SSH.NET dans l’adapter Infrastructure ;
- convertir les exceptions techniques vers des erreurs Application compréhensibles.

Ne laisse pas les objets `SshClient` fuiter hors de la couche Infrastructure.

---

# 12. SSH Jump Host / Bastion

Supporter :

```text
HostDeck
   ↓ SSH
Bastion
   ↓ forwarded/tunneled connection
Target VPS
```

Le monitoring ne doit pas connaître le mode réel de connexion.

API conceptuelle :

```csharp
await using var connection = await sshConnectionFactory.ConnectAsync(
    server,
    cancellationToken);
```

Prévoir :

- plusieurs VPS partageant un bastion ;
- reuse de connexions lorsque sûr et pertinent ;
- pooling raisonnable ;
- limite de connexions/sessions ;
- timeout ;
- backoff exponentiel ;
- keep-alive ;
- invalidation ;
- shutdown propre ;
- cancellation complète.

L’implémentation peut utiliser le port forwarding SSH.NET ou une autre primitive officiellement supportée, mais doit être vérifiée avec Context7 et testée.

Si le bastion tombe :

```text
Bastion         Offline
Targets         GatewayUnavailable
```

Ne pas générer un incident `Offline` indépendant pour chaque cible si la cause racine est clairement le bastion.

Prévoir une corrélation d’incident simple permettant d’éviter une tempête d’alertes.

---

# 13. Docker

Prévoir Docker dès l’architecture V1.

Utiliser :

- `Docker.DotNet` si toujours maintenu et adapté au moment de l’implémentation ;
- sinon un client Docker Engine .NET maintenu et vérifié via Context7 / docs officielles ;
- toujours derrière une abstraction `IContainerRuntime`.

Ne pas parser `docker ps` partout.

Supporter :

- Docker info ;
- containers ;
- status ;
- health ;
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
- ou un tunnel SSH contrôlé ;
- ne jamais exposer le socket Docker publiquement sans protection ;
- ne jamais désactiver TLS ou les contrôles de sécurité sans raison démontrée.

Préparer l’architecture pour Podman plus tard.

Les modèles Docker SDK restent confinés à Infrastructure.

---

# 14. Credentials

Créer `ICredentialStore`.

Utiliser les mécanismes natifs du système :

- macOS Keychain ;
- Windows Credential Manager ;
- Linux Secret Service / keyring.

Choisir l’implémentation après vérification Context7 / documentation officielle / maintenance du package.

Ne jamais stocker en clair dans SQLite :

- private key ;
- password ;
- passphrase ;
- API secret ;
- token sensible.

SQLite ne conserve qu’un identifiant ou une référence de credential.

Ne jamais logger de secret.

Prévoir :

- lecture ;
- écriture ;
- mise à jour ;
- suppression ;
- absence de credential ;
- keyring indisponible ;
- permissions refusées ;
- migration future des credentials.

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

Les commandes envoyées par SSH doivent être fixes, contrôlées et non construites depuis des fragments arbitraires fournis par l’utilisateur.

Ne pas utiliser de shell interpolation dangereuse.

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
CpuStatParser
MemInfoParser
LoadAverageParser
NetworkDevParser
DiskUsageParser
UptimeParser
OsReleaseParser
```

Les parsers doivent être :

- purs autant que possible ;
- indépendants de SSH ;
- indépendants d’Avalonia ;
- testables avec fixtures ;
- robustes face aux lignes inconnues ;
- explicites sur les erreurs de format.

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

L’implémenter comme service de fond avec un lifecycle clair, idéalement via `BackgroundService` / `IHostedService` lorsque pertinent.

Utiliser :

- `CancellationToken` ;
- `PeriodicTimer` ;
- `SemaphoreSlim` ;
- `Task.WhenAll` ou traitement borné ;
- `Channel<T>` pour découpler collecte/persistence si utile ;
- timeouts explicites ;
- backoff avec jitter lorsque pertinent.

Ne pas lancer des `Task.Run` sans ownership.

Chaque tâche de fond doit avoir :

- un owner ;
- un `CancellationToken` ;
- un arrêt propre ;
- une gestion d’erreur ;
- un logging ;
- une politique de retry bornée.

Ne jamais utiliser `async void`, sauf event handlers UI strictement nécessaires.

Ne jamais créer de boucle infinie sans cancellation.

---

# 18. SQLite

Utiliser **Microsoft.Data.Sqlite** sauf raison technique démontrée après analyse.

Points importants :

- SQLite n’offre pas de véritables I/O asynchrones via `Microsoft.Data.Sqlite` ;
- ne pas supposer que `ExecuteReaderAsync` rend l’I/O SQLite réellement asynchrone ;
- utiliser WAL lorsque pertinent ;
- garder les transactions courtes ;
- éviter de bloquer le thread UI ;
- sérialiser/organiser les écritures si nécessaire ;
- mesurer avant d’ajouter une abstraction complexe.

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

- migrations versionnées ;
- `PRAGMA foreign_keys = ON` ;
- WAL si validé ;
- indexes ;
- transactions ;
- pagination ;
- retention ;
- pruning ;
- agrégation ;
- requêtes paramétrées uniquement ;
- commandes et readers toujours disposés correctement.

Les persistence records sont distincts des entities Domain.

Pas d’ORM lourd par défaut. Si un micro-ORM est envisagé, justifier son apport et vérifier sa maintenance/licence.

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

Le repository renvoie des datasets adaptés à la largeur du graphique et à la plage temporelle.

Ne jamais charger des centaines de milliers de points inutilement.

Conserver lorsque pertinent :

- current ;
- min ;
- average ;
- max.

Prévoir une stratégie de downsampling :

- buckets temporels ;
- min/avg/max ;
- conservation des extrêmes ;
- nombre maximum de points par série ;
- requêtes SQL adaptées ;
- cache borné uniquement si mesuré utile.

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

Un `Incident` contient :

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

Le moteur doit éviter :

- duplication du même incident à chaque poll ;
- flapping excessif ;
- tempête d’incidents liée à un bastion ;
- notifications répétées sans cooldown.

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

Prévoir :

- règles globales ;
- règles par groupe ;
- règles par serveur ;
- validation ;
- priorité/override si nécessaire ;
- cooldown ;
- durée minimale avant déclenchement ;
- recovery condition.

---

# 22. Architecture Presentation / Avalonia

Structure recommandée :

```text
HostDeck.Presentation/
├── ViewModels/
│   ├── Overview/
│   ├── Infrastructure/
│   ├── HostDetails/
│   ├── Incidents/
│   ├── Alerts/
│   ├── Reports/
│   ├── Topology/
│   ├── LiveData/
│   ├── Docker/
│   └── Settings/
│
├── Views/
│   ├── Overview/
│   ├── Infrastructure/
│   ├── HostDetails/
│   ├── Incidents/
│   ├── Alerts/
│   ├── Reports/
│   ├── Topology/
│   ├── LiveData/
│   ├── Docker/
│   └── Settings/
│
├── Controls/
├── Charts/
├── Themes/
├── Navigation/
├── Dialogs/
├── Converters/
└── State/
```

Utiliser MVVM proprement :

- Views aussi passives que possible ;
- pas de logique métier dans code-behind ;
- code-behind toléré pour logique purement visuelle/interaction locale difficile à exprimer proprement en binding ;
- commandes asynchrones via toolkit ou implémentation équivalente ;
- état de chargement, erreur, empty state explicites ;
- subscriptions désabonnées correctement.

Ne pas mettre toute l’UI dans `MainWindow.axaml`.

Chaque écran doit avoir son propre ViewModel et son propre état de présentation.

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

Ne pas laisser les styles Avalonia par défaut dominer l’apparence finale.

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

- largeur réduite ;
- icônes centrées ;
- état actif visible ;
- tooltip ;
- pas de labels permanents ;
- sections séparées ;
- compteur incidents possible ;
- compact ;
- navigation clavier cohérente ;
- focus visuel propre sans style web générique.

Créer un contrôle réutilisable si nécessaire, par exemple `RailButton`.

---

# 25. Layout principal

```text
┌──────┬──────────────────────────────────────────────────┐
│ Rail │ Topbar                                           │
│      ├──────────────────────────────────────────────────┤
│      │ Secondary nav │ Main content                    │
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

Utiliser un `Grid` et un `GridSplitter` ou une approche équivalente fidèle à la maquette.

Le panneau ne doit pas être ouvert s’il n’y a aucune sélection.

Le panneau doit :

- s’ouvrir au clic sur une ligne ;
- pouvoir se fermer ;
- conserver des proportions raisonnables ;
- ne pas casser le layout au resize ;
- respecter une largeur minimale/maximale ;
- être redimensionnable depuis le bon côté selon la maquette.

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
- menu contextuel ;
- navigation clavier ;
- scroll performant.

Utiliser **Avalonia `DataGrid`** ou un contrôle équivalent gratuit et maintenu.

Ne pas dépendre d’un composant Avalonia Pro payant pour une fonction essentielle du projet open source.

Configurer précisément :

- `RowHeight` dense ;
- headers compacts ;
- styles de cellules ;
- sélection ;
- alternance si présente dans la maquette ;
- colonnes template pour sparkline/status/tags ;
- virtualisation disponible ;
- pas de contrôles lourds inutiles dans chaque cellule.

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

Toutes les actions doivent être exposées via commandes du ViewModel.

L’UI ne doit jamais modifier directement un repository.

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

Les valeurs qui changent fréquemment ne doivent pas provoquer un rerender massif de toute la vue.

Mettre à jour uniquement les lignes/séries réellement modifiées.

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

Pour les logs :

- streaming cancelable ;
- buffer borné ;
- pause/autoscroll ;
- filtre texte si pertinent ;
- ne pas charger des millions de lignes en mémoire ;
- fermer proprement le stream Docker.

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

Les champs sensibles ne doivent jamais être préremplis avec une valeur secrète brute venant de SQLite.

Le bouton de test de connexion doit :

- afficher un état en cours ;
- pouvoir être annulé ;
- distinguer DNS, timeout, auth, host key, bastion, target, Docker si testé ;
- ne pas bloquer le thread UI.

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

Ne pas utiliser une librairie de charts générique si elle empêche d’atteindre le design des maquettes.

Une librairie externe n’est acceptable que si elle :

- est maintenue ;
- est compatible Avalonia 12/.NET 10 ;
- permet un styling fin ;
- reste performante ;
- n’impose pas un rendu visuel incompatible ;
- a une licence adaptée au projet.

---

# 32. Avalonia — moteur de graphiques custom

Préférer un moteur léger custom basé sur le rendering Avalonia lorsque c’est nécessaire pour la fidélité visuelle.

Architecture :

```text
Charts/
├── Models/
│   ├── ChartSeries.cs
│   ├── ChartPoint.cs
│   ├── TimeRange.cs
│   └── ChartViewport.cs
│
├── Rendering/
│   ├── AxisRenderer.cs
│   ├── GridRenderer.cs
│   ├── LineRenderer.cs
│   ├── AreaRenderer.cs
│   ├── StackedAreaRenderer.cs
│   ├── ThresholdRenderer.cs
│   └── ScaleCalculator.cs
│
├── Controls/
│   ├── TimeSeriesChart.cs
│   ├── StackedAreaChart.cs
│   └── Sparkline.cs
│
└── Interaction/
    ├── CrosshairState.cs
    ├── TooltipState.cs
    └── TimeRangeSelector.cs
```

Pour les contrôles custom :

- hériter de `Control` lorsque le dessin direct est approprié ;
- override `Render(DrawingContext context)` pour le rendu standard ;
- utiliser des propriétés Avalonia adaptées ;
- invalider le rendu uniquement lorsque nécessaire ;
- éviter les allocations dans `Render` ;
- réutiliser brushes, pens, geometries et données calculées lorsque possible ;
- mesurer avant d’utiliser des APIs de composition plus complexes.

Pour des visualisations réellement temps réel et coûteuses :

- étudier `CompositionCustomVisualHandler` ;
- l’utiliser uniquement si les mesures montrent un bénéfice ;
- ne jamais lire directement de state UI mutable depuis le render thread ;
- communiquer avec le renderer via des snapshots immutables/messages.

Toujours vérifier l’API exacte de la version Avalonia utilisée via Context7.

---

# 33. Performance des charts

Ne pas recréer toutes les ressources visuelles à chaque frame.

Prévoir :

- cache de géométrie ;
- reuse ;
- datasets bornés ;
- downsampling ;
- invalidation ciblée ;
- snapshots immutables de séries lorsque utile ;
- calcul des scales hors du hot path si possible ;
- réduction du nombre de labels selon largeur disponible.

Pour les sparklines de table :

- renderer ultra léger ;
- pas de labels ;
- pas d’axes ;
- dataset court ;
- pas de tooltip dans chaque cellule si trop coûteux ;
- éviter un ViewModel complexe par point ;
- éviter de créer des centaines d’objets visuels par cellule.

Éviter un rafraîchissement fixe à 60 FPS si les données n’arrivent qu’une fois par seconde ou moins.

Le framerate doit suivre le besoin réel.

---

# 34. Design System Avalonia

Créer un thème HostDeck centralisé.

Structure :

```text
Themes/
├── HostDeckTheme.axaml
├── Colors.axaml
├── Typography.axaml
├── Spacing.axaml
├── Sizes.axaml
├── Controls.axaml
├── DataGrid.axaml
├── Severity.axaml
└── Icons.axaml
```

Tokens conceptuels :

```text
SurfaceBackground
SurfaceRail
SurfacePanel
SurfaceRaised

BorderSubtle

TextPrimary
TextSecondary
TextMuted

Accent
Selection

StatusOnline
StatusOffline
StatusWarning

SeverityInformation
SeverityWarning
SeverityAverage
SeverityHigh
SeverityCritical
```

Utiliser des resources dynamiques/statiques Avalonia de manière cohérente.

Pas de couleurs dispersées en dur dans les Views.

Pas de tailles ou marges magiques répétées partout.

Centraliser :

- palette ;
- typographie ;
- rayons ;
- border thickness ;
- row heights ;
- icon sizes ;
- rail width ;
- panel widths ;
- spacing scale.

---

# 35. Custom Controls

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

Choisir le bon type Avalonia :

- `UserControl` pour composer des contrôles existants ;
- `TemplatedControl` pour un contrôle réutilisable et thémable ;
- `Control` + `Render` pour le dessin custom ;
- attached properties uniquement si elles simplifient réellement la composition.

Ne pas transformer chaque petit élément en custom control sans raison.

---

# 36. Avalonia threading

Avalonia utilise un modèle UI mono-thread.

Toutes les interactions avec les contrôles/propriétés UI doivent rester sur le dispatcher approprié.

Règles :

- ne jamais modifier un contrôle Avalonia depuis un worker thread ;
- utiliser `Dispatcher.UIThread.Post(...)`, `InvokeAsync(...)` ou le dispatcher du `AvaloniaObject` concerné selon l’API actuelle ;
- préférer les bindings/ViewModels aux modifications directes de contrôles ;
- les opérations SSH/Docker/SQLite longues doivent rester hors du thread UI ;
- aucune attente synchrone `.Result` / `.Wait()` sur des tâches asynchrones depuis l’UI ;
- pas de deadlock par capture de contexte mal maîtrisée.

Pour les custom controls et librairies internes, tenir compte du support des dispatchers introduit dans Avalonia récent et vérifier l’API exacte avec Context7.

---

# 37. Performance générale

Objectifs :

- scroll fluide ;
- CPU faible au repos ;
- update ciblée ;
- tables capables de centaines de lignes ;
- graphs fluides ;
- pas de refresh global continu ;
- aucune freeze UI lors des connexions SSH/Docker ;
- mémoire stable sur de longues sessions.

Éviter :

- `PropertyChanged` global sur toute la page ;
- remplacement complet d’une collection à chaque poll ;
- gros recalculs chaque seconde ;
- allocations massives ;
- duplication des métriques ;
- historiques non bornés ;
- `Task.Run` répétés sans contrôle ;
- timers UI par ligne de table ;
- subscriptions Reactive/EventHandler jamais libérées.

Préférer :

- updates incrémentales ;
- `ObservableCollection` uniquement là où elle est appropriée ;
- batch updates si nécessaire ;
- modèles de lecture immutables ;
- virtualisation ;
- caches bornés ;
- profiling avant optimisation complexe.

---

# 38. Erreurs

Créer une hiérarchie d’erreurs métier/technique claire.

Exemples :

```text
ValidationException
RepositoryException
SshConnectionException
SshAuthenticationException
HostKeyVerificationException
GatewayUnavailableException
DockerException
MonitoringException
CredentialException
NotificationException
```

Ne pas utiliser les exceptions comme flux métier normal lorsque cela peut être représenté proprement par un résultat.

Pour les opérations où plusieurs issues sont attendues, un type `Result<T>` interne peut être envisagé si cela améliore réellement la clarté, sans ajouter une dépendance lourde inutile.

Règles :

- conserver l’exception originale comme `InnerException` ;
- enrichir le contexte ;
- ne pas avaler les exceptions ;
- ne pas exposer directement une stack trace technique à l’utilisateur ;
- mapper les erreurs vers des messages UI français ;
- logguer au bon niveau.

---

# 39. Logging

Utiliser `Microsoft.Extensions.Logging`.

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
- result ;
- retry_count ;
- exception type.

Aucun secret dans les logs.

Prévoir :

- logs fichier local avec rotation si une dépendance est retenue ;
- niveau configurable ;
- logs structurés ;
- corrélation d’opérations lorsque utile.

Si Serilog ou un autre provider est ajouté, justifier sa présence et vérifier maintenance/licence.

---

# 40. Tests Domain

Utiliser **xUnit** sauf raison forte de choisir autre chose.

Tester :

- validations ;
- statuses ;
- thresholds ;
- severities ;
- incidents ;
- connection mode ;
- cooldown ;
- retention ;
- value objects ;
- transitions d’état invalides.

Les tests Domain doivent être rapides et sans dépendance Infrastructure.

---

# 41. Tests Application

Utiliser des fakes/mocks pour les ports.

Choisir NSubstitute, Moq ou fakes manuels uniquement après vérification de maintenance/licence ; préférer les fakes manuels lorsque simples.

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
- alert cooldown ;
- cancellation ;
- retry borné.

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
- plusieurs partitions ;
- espaces/tabs inattendus ;
- lignes supplémentaires ;
- nombres très grands ;
- format locale indépendant.

Les parsers doivent utiliser `CultureInfo.InvariantCulture` pour les formats techniques lorsque nécessaire.

---

# 43. Tests SQLite

Utiliser une vraie base SQLite temporaire.

Tester :

- migrations ;
- insert ;
- update ;
- delete ;
- transactions ;
- indexes critiques ;
- queries temporelles ;
- retention ;
- foreign keys ;
- WAL/configuration ;
- rollback ;
- contraintes ;
- pruning ;
- downsampling.

Ne pas mocker SQLite pour les tests du repository SQLite lui-même.

---

# 44. Tests SSH

Unit tests :

- fake `ISshConnection` ;
- fake factory ;
- timeout ;
- cancellation ;
- command errors ;
- auth failure mapping ;
- host key failure mapping ;
- gateway unavailable.

Integration :

```text
Client
 ↓
SSH Bastion
 ↓
SSH Target
```

Utiliser des containers dédiés dans un environnement de test si pertinent.

Les integration tests réseau doivent être séparés des unit tests et pouvoir être désactivés localement si Docker n’est pas disponible.

---

# 45. Tests Docker

Unit tests :

- fake `IContainerRuntime` ;
- containers ;
- stats ;
- logs ;
- restart ;
- errors ;
- cancellation ;
- stream closure.

Integration tests séparés avec Docker Engine réel dans CI dédiée si raisonnable.

Ne pas dépendre du Docker personnel de l’utilisateur pour exécuter les unit tests.

---

# 46. Tests UI Avalonia

Tester les comportements importants sans dépendre uniquement du rendu pixel-perfect.

Utiliser les capacités de test/headless Avalonia actuelles, par exemple `Avalonia.Headless` / intégration xUnit si toujours supportée, après vérification Context7.

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
- empty states ;
- commandes async ;
- resize logique des panneaux ;
- états disabled/busy.

Ajouter lorsque pertinent des tests visuels/screenshot sur quelques composants stables, sans rendre toute la suite fragile aux différences de rendu inter-OS.

La validation visuelle humaine avec les maquettes reste obligatoire.

---

# 47. Qualité de code stricte

Exigence niveau production.

Interdit :

- énorme `MainWindow.axaml.cs` ;
- énorme `App.axaml.cs` ;
- God Services ;
- Service Locator global ;
- singletons métier arbitraires ;
- `Task.Run` sans lifecycle ;
- `async void` hors events UI ;
- exceptions ignorées ;
- `catch { }` vide ;
- `.Result` / `.Wait()` dans les flux async ;
- magic numbers ;
- SQL dans Presentation ;
- SSH dans Presentation ;
- Docker dans Presentation ;
- fichiers de milliers de lignes ;
- duplication ;
- interfaces inutiles ;
- classes `Utils` fourre-tout ;
- état mutable global ;
- `CancellationToken.None` utilisé par défaut dans les opérations longues sans raison ;
- `ConfigureAwait(false)` dispersé au hasard sans comprendre le contexte.

Préférer :

- petites APIs ;
- responsabilités nettes ;
- records/value objects ;
- composition ;
- injection explicite ;
- cancellation propagation ;
- méthodes courtes ;
- `IDisposable` / `IAsyncDisposable` corrects ;
- `await using` pour ressources asynchrones ;
- immutabilité lorsque pertinente ;
- constructors gardant les invariants ;
- `required` members uniquement si cela améliore la sûreté du modèle.

---

# 48. Analyse statique / compilation stricte

Configurer `Directory.Build.props` avec au minimum :

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
</PropertyGroup>
```

Adapter si une dépendance externe génère un warning non corrigeable, mais ne jamais désactiver globalement des règles sans justification.

Avant une tâche terminée :

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

Ajouter des analyzers supplémentaires uniquement si utiles, maintenus et compatibles.

Aucun warning sérieux ne doit rester sans justification.

---

# 49. Concurrence / race conditions

.NET ne dispose pas d’un équivalent direct universel à `go test -race` pour ce projet.

Il faut donc compenser avec :

- design évitant le partage mutable ;
- collections concurrentes uniquement lorsque pertinentes ;
- synchronisation explicite ;
- tests de concurrence ;
- tests de stress ;
- cancellation tests ;
- instrumentation ;
- review ciblée des sections concurrentes.

Tester particulièrement :

- `FleetMonitoringCoordinator` ;
- pools SSH ;
- Docker stats streams ;
- caches ;
- event publication ;
- collection updates ;
- shutdown application ;
- prune + read simultanés ;
- retry/cancellation simultanés.

Ne pas utiliser `lock` partout sans réflexion.

Favoriser les snapshots immutables et les files de messages (`Channel<T>`) lorsque cela simplifie la synchronisation.

---

# 50. Mémoire / leaks

Vérifier :

- tasks qui continuent après fermeture ;
- `PeriodicTimer` non stoppés/disposés ;
- `CancellationTokenSource` non disposés ;
- event handlers non désabonnés ;
- observables/subscriptions non disposés ;
- connexions SSH non fermées ;
- forwarded ports non arrêtés ;
- Docker streams non fermés ;
- SQLite readers/connections non disposés ;
- caches sans limite ;
- datasets chart non bornés ;
- ViewModels retenus par des events globaux ;
- bitmaps/images non libérés ;
- logs en mémoire non bornés.

Utiliser lorsque nécessaire :

```text
dotnet-counters
dotnet-trace
dotnet-gcdump
dotnet-dump
```

Faire des tests de session longue simulant plusieurs centaines de cycles de monitoring.

---

# 51. Sécurité

Exigences :

- secrets hors SQLite ;
- secrets hors logs ;
- host key verification ;
- validation des entrées ;
- requêtes SQL paramétrées ;
- commandes SSH internes contrôlées ;
- pas d’injection shell ;
- pas de socket Docker exposé publiquement ;
- credential storage natif ;
- accès Docker sécurisé ;
- permissions minimales ;
- pas de bypass TLS/SSH silencieux ;
- pas de désactivation de validation de certificat “temporaire” laissée en production.

Ajouter un threat model synthétique dans `docs/SECURITY.md`.

---

# 52. CI

Créer GitHub Actions.

Pipeline principal :

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

Ajouter :

- cache NuGet ;
- publication des résultats de tests ;
- couverture si raisonnable ;
- tests d’intégration séparés ;
- build multiplateforme.

Build au minimum sur :

- Linux ;
- Windows ;
- macOS.

Vérifier les Runtime Identifiers actuels avant packaging.

Préparer des builds/publish pour :

```text
win-x64
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

uniquement si supportés et réellement testés.

Ne pas prétendre supporter une cible non testée.

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

- README.md ;
- AGENTS.md ;
- CONTRIBUTING.md ;
- SECURITY.md.

La documentation technique est en anglais.

Documenter :

- architecture ;
- dépendances ;
- lifecycle ;
- décisions importantes ;
- migrations ;
- modèle de sécurité ;
- développement local ;
- lancement ;
- tests ;
- packaging.

---

# 54. AGENTS.md

Inclure au minimum :

1. communication en français ;
2. code en anglais ;
3. UI en français ;
4. commentaires en français ;
5. docs techniques en anglais ;
6. Context7 obligatoire ;
7. .NET 10 LTS ;
8. Avalonia uniquement pour UI ;
9. aucun WebView / frontend web ;
10. Domain indépendant d’Avalonia ;
11. DTO séparés ;
12. persistence records séparés ;
13. pas de SQL/SSH/Docker dans Presentation ;
14. `CancellationToken` propagé ;
15. tâches de fond contrôlées ;
16. secrets hors SQLite ;
17. tests obligatoires ;
18. warnings as errors ;
19. `dotnet format` ;
20. maquettes = référence visuelle ;
21. rail vertical obligatoire ;
22. charts type Netdata ;
23. inspection visuelle obligatoire ;
24. pas de refactor massif sans justification ;
25. pas de dépendance ajoutée sans vérification ;
26. pas de composant Avalonia Pro obligatoire pour une feature essentielle open source ;
27. pas d’opération bloquante sur le thread UI ;
28. pas de `.Result` / `.Wait()` ;
29. resources disposées ;
30. documentation mise à jour avec les changements d’architecture.

---

# 55. Écrans V1

Créer au minimum :

## Overview

- état global ;
- disponibilité ;
- incidents ;
- capacité ;
- hosts critiques ;
- graphiques synthétiques ;
- accès rapide aux détails.

## Infrastructure

- rail ;
- groupes/environnements ;
- hosts table ;
- metrics ;
- sparklines ;
- details panel.

## Host Details

- état ;
- identité ;
- CPU ;
- RAM ;
- disk ;
- network ;
- load ;
- uptime ;
- historique ;
- Docker summary ;
- incidents liés.

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

## Reports

- disponibilité ;
- SLA ;
- tendances ;
- capacité ;
- résumés temporels.

## Topology

- représentation claire des hosts ;
- jump hosts ;
- dépendances ;
- états ;
- incidents ;
- interactions adaptées à la maquette.

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

Préparer l’architecture pour :

- systemd ;
- journald ;
- process list ;
- ports ;
- HTTP/HTTPS ;
- TLS expiration ;
- terminal SSH ;
- topology enrichie ;
- autodiscovery ;
- custom dashboards ;
- Podman ;
- HostDeck agent.

Ne pas implémenter prématurément.

Les abstractions V1 ne doivent cependant pas empêcher ces évolutions.

---

# 57. Workflow agent

Avant de coder :

1. inspecter le repo ;
2. lire ce prompt ;
3. inspecter toutes les maquettes ;
4. utiliser Context7 ;
5. vérifier .NET 10 ;
6. vérifier Avalonia 12+ ;
7. vérifier DataGrid ;
8. vérifier custom rendering Avalonia ;
9. vérifier threading/Dispatcher Avalonia ;
10. vérifier SSH.NET ;
11. vérifier Docker client ;
12. vérifier Microsoft.Data.Sqlite ;
13. vérifier credential storage ;
14. définir architecture ;
15. définir Domain ;
16. définir DTO ;
17. définir interfaces ;
18. définir SQLite ;
19. définir SSH/jump host ;
20. définir Docker ;
21. définir monitoring coordinator ;
22. définir Presentation/MVVM ;
23. définir charts Avalonia custom ;
24. définir Design System ;
25. définir tests ;
26. me présenter le plan en français.

Implémenter ensuite par étapes cohérentes.

Ordre recommandé :

```text
Foundation
→ Domain
→ Application
→ Persistence
→ SSH
→ Monitoring
→ Incidents/Alerts
→ Docker
→ Presentation shell
→ Infrastructure screen
→ Host details
→ Incidents
→ Live Data
→ Docker UI
→ Alerts
→ Reports/Topology
→ Settings
→ Polish/performance
→ Packaging/CI/docs
```

À chaque étape :

- build ;
- tests ;
- pas de warnings ;
- documentation si décision structurante ;
- commit cohérent si le workflow le permet.

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
14. vérifier loading/empty/error ;
15. vérifier navigation clavier ;
16. vérifier focus ;
17. vérifier hover/selected/disabled ;
18. vérifier Windows/macOS/Linux si possible.

La validation visuelle est obligatoire.

Pour automatiser une partie du contrôle :

- utiliser les capacités screenshot/headless Avalonia si fiables ;
- garder des captures de référence ;
- ne pas substituer ces tests à une comparaison visuelle réelle.

---

# 59. Critères de validation finale

La V1 n’est terminée que si :

- `dotnet restore` OK ;
- `dotnet format --verify-no-changes` OK ;
- build Release OK ;
- tests OK ;
- warnings = 0 ou justifications explicites ;
- architecture respectée ;
- UI Avalonia uniquement ;
- aucun WebView ;
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
- pas de tâches de fond orphelines connues ;
- pas de croissance mémoire anormale ;
- pas de freeze UI lors d’I/O ;
- cancellation/shutdown propres ;
- UI comparable aux maquettes ;
- documentation à jour ;
- CI multiplateforme fonctionnelle ;
- packaging au minimum documenté et reproductible.

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

Le résultat final doit être une interprétation desktop **C#/.NET 10 + Avalonia** des maquettes validées.

Les maquettes sont la référence visuelle principale.

Le backend/core doit rester propre, testable, concurrent, cancelable et sécurisé.

L’UI doit être dense, technique, premium et ressembler à un vrai outil de monitoring, avec des graphiques proches du style Netdata et un rail vertical compact.

L’application ne doit jamais ressembler à une webapp emballée dans un exécutable.

---

# 61. Contraintes C#/.NET supplémentaires

## Nullable

`Nullable` doit être activé partout.

Interdit de neutraliser les warnings avec `!` de manière arbitraire.

Chaque nullability suppression doit être justifiée par un invariant réel.

## Cancellation

Toute opération réseau ou longue doit accepter un `CancellationToken`.

La fermeture de l’application doit annuler :

- monitoring ;
- SSH en cours ;
- Docker streams ;
- polling ;
- retries ;
- refresh UI programmés ;
- opérations de test de connexion.

## IDisposable / IAsyncDisposable

Tout objet possédant une ressource doit être libéré correctement :

- SSH client ;
- stream ;
- Docker response stream ;
- SQLite connection/reader ;
- timer ;
- CTS ;
- subscription ;
- fichier ;
- ressource native.

## Collections

Ne pas exposer une collection mutable interne sans nécessité.

Préférer :

```text
IReadOnlyList<T>
IReadOnlyCollection<T>
ImmutableArray<T>
```

lorsque pertinent.

## Date/heure

Préférer `DateTimeOffset` pour les timestamps persistés/échangés.

Stocker en UTC dans SQLite.

Convertir en heure locale uniquement dans Presentation.

## IDs

Utiliser `Guid` et des wrappers fortement typés lorsque cela apporte une vraie sécurité :

```csharp
public readonly record struct ServerId(Guid Value);
```

Ne pas transformer tous les types primitifs en wrappers sans bénéfice.

---

# 62. MVVM strict mais pragmatique

Le MVVM doit aider la maintenabilité et non ajouter de la cérémonie.

Chaque ViewModel doit :

- représenter un écran ou composant cohérent ;
- recevoir ses dépendances par constructeur ;
- ne pas connaître `Window` directement ;
- ne pas faire de SQL ;
- ne pas créer de client SSH ;
- ne pas créer de client Docker ;
- exposer des commandes ;
- exposer `IsLoading`, erreurs et état vide lorsque pertinent ;
- disposer ses subscriptions.

Créer des services Presentation dédiés si nécessaire :

```text
INavigationService
IDialogService
IClipboardService
IFilePickerService
IUiDispatcher
```

Ces services doivent être petits et centrés sur des capacités UI.

Ne pas créer un `IAnythingService` pour chaque méthode.

---

# 63. Navigation desktop

La navigation doit être interne à la fenêtre principale et instantanée.

Prévoir :

- rail principal ;
- navigation secondaire selon écran ;
- contenu courant ;
- historique de sélection seulement si utile ;
- état de sélection conservé intelligemment ;
- raccourcis clavier ;
- focus cohérent.

Éviter une architecture de navigation inspirée du web avec URLs/routes si elle n’apporte rien.

Une petite abstraction de navigation par enum/page key suffit si elle reste claire.

---

# 64. État temps réel et bus d’événements

Le monitoring produit des changements fréquents.

Ne pas coupler directement le `FleetMonitoringCoordinator` aux ViewModels.

Prévoir un mécanisme interne sobre :

```text
MonitoringEvent
MetricUpdatedEvent
ServerStatusChangedEvent
IncidentChangedEvent
DockerContainerChangedEvent
```

Implémentation possible :

- `Channel<T>` ;
- event hub fortement typé ;
- observable interne léger.

Contraintes :

- subscriptions disposables ;
- pas de fuite ;
- pas de global static event bus ;
- backpressure si volume important ;
- coalescing des updates UI si nécessaire ;
- aucun événement contenant de secret.

---

# 65. Persistance des paramètres UI

Conserver localement les préférences non sensibles :

- taille fenêtre ;
- position si valide ;
- largeur panel ;
- filtre courant si utile ;
- tri ;
- plage temporelle ;
- thème si plusieurs thèmes existent ;
- options de notification.

Ne jamais persister une position hors écran après changement de moniteur.

Valider les valeurs au chargement.

---

# 66. Packaging desktop

HostDeck doit pouvoir être distribué proprement.

Préparer selon plateforme :

## Windows

- executable/publish ;
- icône ;
- metadata ;
- packaging installer adapté ;
- code signing préparé/documenté.

## macOS

- `.app` ;
- bundle metadata ;
- icône ;
- notarization/signing préparés/documentés ;
- Apple Silicon support prioritaire.

## Linux

- package ou archive reproductible ;
- `.desktop` ;
- icône ;
- AppImage/Flatpak/deb/rpm seulement après analyse et sans multiplier inutilement les formats en V1.

Ne pas mélanger le cœur produit avec la logique de packaging.

Créer `scripts/` et workflows dédiés.

---

# 67. Définition de “même qualité”

Le changement Go/Fyne → C#/Avalonia ne doit entraîner **aucune baisse d’exigence**.

Au contraire, exploiter les forces de .NET/Avalonia :

- MVVM mature ;
- binding ;
- DataGrid ;
- DI/Hosting ;
- `async`/`await` ;
- tooling Roslyn ;
- analyzers ;
- headless testing ;
- rendering custom ;
- packaging multiplateforme.

La qualité attendue reste :

```text
production-grade
clean architecture
strict typing
high testability
bounded concurrency
secure credentials
responsive desktop UI
high visual fidelity
low idle CPU
stable memory usage
reliable shutdown
maintainable codebase
```

Ne jamais répondre à une difficulté Avalonia par une simplification visuelle qui dégrade les maquettes sans avoir d’abord cherché une solution custom propre.
