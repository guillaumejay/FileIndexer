# File Indexer

Indexeur de fichiers haute performance pour NAS avec interface web.

## Fonctionnalités

- **Scan parallèle** : Indexation de 200k+ fichiers en quelques minutes
- **Recherche instantanée** : FTS5 avec temps de réponse < 50ms
- **Multi-plateforme** : Windows, Linux, macOS
- **Interface web moderne** : Blazor Server avec progression en temps réel

## Prérequis

- .NET 10 SDK

## Installation

```bash
# Cloner/copier le projet
cd FileIndexer

# Restaurer les packages
dotnet restore

# Lancer l'application
dotnet run
```

L'application sera accessible sur http://localhost:5000

## Configuration

Modifier `appsettings.json` :

```json
{
  "AppSettings": {
    "DefaultScanPath": "/chemin/vers/nas",
    "DatabasePath": "fileindex.db",
    "ScanParallelism": 64,
    "ScanBatchSize": 500
  }
}
```

### Paramètres

| Paramètre | Description | Défaut |
|-----------|-------------|--------|
| `DefaultScanPath` | Chemin pré-rempli dans l'interface | vide |
| `DatabasePath` | Emplacement de la base SQLite | `fileindex.db` |
| `ScanParallelism` | Nombre de threads parallèles | 64 |
| `ScanBatchSize` | Taille des lots d'insertion en DB | 500 |

## Chemins supportés

### Windows
```
C:\Users\...
\\serveur\partage
Z:\montage-nas
```

### Linux / macOS
```
/mnt/nas
/media/partage
/Volumes/NAS
```

## Utilisation

1. **Configurer le chemin** : Entrer le chemin du NAS dans le champ de texte
2. **Lancer le scan** : Cliquer sur "Démarrer le scan"
3. **Rechercher** : Utiliser la barre de recherche (recherche en temps réel)

### Recherche

- Recherche par nom de fichier avec préfixe (ex: `rapport` trouve `rapport-2024.pdf`)
- Cliquer sur une extension dans les stats pour filtrer
- Cliquer sur une ligne pour copier le chemin

## Architecture

```
FileIndexer/
├── Models/              # Modèles de données
├── Data/                # Accès SQLite + FTS5
├── Services/            # Scanner + Recherche
├── Components/          # Interface Blazor
│   ├── Layout/
│   └── Pages/
└── wwwroot/css/         # Styles
```

## Performance

| Volume | Temps de scan* | Temps de recherche |
|--------|---------------|-------------------|
| 50k fichiers | ~2 min | < 20ms |
| 200k fichiers | ~8 min | < 50ms |
| 500k fichiers | ~20 min | < 100ms |

*Dépend de la latence réseau du NAS

## Publication

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/win

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux

# macOS (Intel)
dotnet publish -c Release -r osx-x64 --self-contained -o ./publish/osx

# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm
```

## Licence

MIT
