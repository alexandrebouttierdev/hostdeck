<div align="center">

# HostDeck

### Supervision d'infrastructure desktop

**Supervision moderne de serveurs Linux, VPS et environnements Docker.**

<br>

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Avalonia](https://img.shields.io/badge/Avalonia-UI-8B5CF6?style=for-the-badge)](https://avaloniaui.net/)
[![Docker](https://img.shields.io/badge/Docker-Supervision-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
[![SSH](https://img.shields.io/badge/SSH-Supervision%20sécurisée-222222?style=for-the-badge&logo=linux&logoColor=white)](https://www.openssh.com/)

<br>

[![Plateformes](https://img.shields.io/badge/Plateformes-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey?style=flat-square)](#)
[![Statut](https://img.shields.io/badge/Statut-Actif-success?style=flat-square)](#)
[![Open Source](https://img.shields.io/badge/Open%20Source-Oui-blue?style=flat-square)](#)

<br><br>

<img src="mockups/dashboard.png" alt="Tableau de bord HostDeck" width="900">

</div>

---

## Présentation

**HostDeck** est une application desktop multiplateforme permettant de superviser et administrer une infrastructure Linux depuis une interface unique.

Connectez vos serveurs via **SSH**, surveillez leurs ressources système, consultez vos environnements Docker, analysez l'historique des métriques et gérez les incidents directement depuis une application native.

HostDeck adopte une approche **local-first** : les données de supervision sont conservées localement et aucun service SaaS ou compte cloud n'est obligatoire.

---

## Pensé pour l'infrastructure

| Fonctionnalité | Description |
|---|---|
| **Linux & VPS** | Supervision de plusieurs serveurs Linux depuis une interface unique |
| **SSH** | Connexions directes et prise en charge des bastions / jump hosts |
| **Docker** | Conteneurs, statistiques, journaux et gestion du cycle de vie |
| **Supervision** | CPU, mémoire, swap, disques, réseau, charge système et disponibilité |
| **Alertes** | Règles, incidents, niveaux de gravité, acquittement et résolution |
| **Données locales** | Historique SQLite sans dépendance à un service cloud |
| **Sécurité** | Identifiants protégés par le trousseau sécurisé du système |
| **Multiplateforme** | Windows, macOS et Linux |

---

## Fonctionnalités

### Supervision des serveurs

- Gestion de plusieurs serveurs Linux
- Organisation par groupes et étiquettes
- Connexions SSH directes
- Connexions via jump host / bastion
- Vérification stricte des clés d'hôte SSH
- Surveillance de la disponibilité des serveurs

### Métriques système

- Utilisation du processeur
- Mémoire vive
- Mémoire swap
- Utilisation des disques
- Charge système
- Activité réseau
- Temps de fonctionnement

### Supervision Docker

- Liste et état des conteneurs
- Statistiques de consommation
- Consultation des journaux
- Démarrage des conteneurs
- Arrêt des conteneurs
- Redémarrage des conteneurs

### Historique et visualisation

- Historique local des métriques
- Conservation configurable des données
- Nettoyage automatique des anciennes données
- Réduction des données historiques
- Graphiques temporels
- Mini-graphiques de tendance

### Alertes et incidents

- Création de règles d'alerte
- Plusieurs niveaux de gravité
- Gestion des incidents
- Acquittement des incidents
- Résolution des incidents
- Notifications desktop

---

## Technologies

| Technologie | Utilisation |
|---|---|
| **C#** | Logique applicative et métier |
| **.NET 10** | Plateforme et environnement d'exécution |
| **Avalonia UI** | Interface desktop multiplateforme |
| **SSH** | Connexion et supervision des serveurs Linux |
| **Docker** | Supervision des conteneurs |
| **Entity Framework Core** | Accès aux données |
| **SQLite** | Stockage local |
| **Trousseau système** | Stockage sécurisé des identifiants |
| **GitHub Actions** | Intégration continue et contrôles qualité |

---

## Architecture

HostDeck utilise une architecture en couches permettant de séparer clairement le domaine métier, les cas d'utilisation, l'infrastructure et l'interface graphique.

```text
┌──────────────────────────────────────────────┐
│              HostDeck.Desktop                │
│        Composition et point d'entrée         │
└──────────────────────┬───────────────────────┘
                       │
┌──────────────────────▼───────────────────────┐
│            HostDeck.Presentation             │
│        Avalonia / Vues / ViewModels          │
└──────────────────────┬───────────────────────┘
                       │
┌──────────────────────▼───────────────────────┐
│            HostDeck.Application              │
│      Cas d'utilisation / DTO / Ports         │
└──────────────────────┬───────────────────────┘
                       │
┌──────────────────────▼───────────────────────┐
│              HostDeck.Domain                 │
│       Entités / Objets valeur / Règles       │
└──────────────────────────────────────────────┘
                       ▲
                       │
┌──────────────────────┴───────────────────────┐
│           HostDeck.Infrastructure            │
│ SSH / Docker / EF Core / SQLite / Trousseau │
│                 Notifications                │
└──────────────────────────────────────────────┘
