## Context

FileIndexer est actuellement un projet Blazor Server monolithique. Pour supporter une version mobile (Android/iOS) en lecture seule, nous devons réorganiser le code en plusieurs projets avec une séparation claire entre :
- Le code partagé (models, data access, services de lecture)
- Le code desktop-only (scanning, opérations fichiers, corbeille)
- Les UI spécifiques (Web vs MAUI)

La base de données SQLite sera synchronisée via OneDrive/Dropbox entre desktop et mobile.

## Goals / Non-Goals

**Goals:**
- Extraire le code réutilisable dans une bibliothèque Core
- Isoler les services desktop-only dans une bibliothèque Desktop
- Créer une application MAUI Blazor Hybrid pour mobile
- Maintenir la compatibilité Web sur Windows/Linux/macOS
- Permettre la consultation de l'index sur mobile (lecture seule)

**Non-Goals:**
- Partager les composants Razor entre Web et MAUI (on copie et adapte)
- Supporter l'indexation sur mobile
- Supporter les opérations de modification (rename, copy, move, delete) sur mobile
- Supporter Linux via MAUI (limitation Microsoft)
- Implémenter la synchronisation du .db (délégué à OneDrive/Dropbox)

## Decisions

### D1: Structure multi-projets avec bibliothèques de classes

```
FileIndexer.sln
├── src/
│   ├── FileIndexer.Core/           ← net10.0
│   ├── FileIndexer.Desktop/        ← net10.0
│   ├── FileIndexer.Web/            ← net10.0 (actuel, adapté)
│   └── FileIndexer.Maui/           ← net10.0-android, net10.0-ios, etc.
```

**Rationale**: Séparation claire des responsabilités. Core peut être référencé par tous, Desktop uniquement par Web.

**Alternatives considérées**:
- Shared project (.shproj) : Moins flexible, pas de vrai binaire
- Tout dans un seul projet avec #if : Trop de complexité conditionnelle

### D2: FileIndexer.Core contient le strict minimum partagé

```
FileIndexer.Core/
├── Models/
│   ├── IndexedFile.cs
│   └── Collection.cs
├── Data/
│   └── IndexDbContext.cs (lecture + écriture)
└── Services/
    ├── SearchService.cs
    └── CollectionService.cs (read-only methods)
```

**Rationale**: Garde Core léger et sans dépendances platform-specific. IndexDbContext reste dans Core car la structure DB est identique.

### D3: FileIndexer.Desktop isole les opérations filesystem

```
FileIndexer.Desktop/
└── Services/
    ├── FileScannerService.cs
    ├── FileOperationsService.cs
    └── Trash/
        ├── ITrashService.cs
        ├── WindowsTrashService.cs
        ├── LinuxTrashService.cs
        └── MacTrashService.cs
```

**Rationale**: Ces services utilisent Process.Start, accès filesystem avancé, et APIs OS-specific. Non pertinents sur mobile.

### D4: MAUI utilise Blazor Hybrid (pas XAML natif)

**Rationale**:
- Réutilisation maximale des compétences Blazor/Razor
- Les composants peuvent être copiés et adaptés facilement
- Pas besoin d'apprendre XAML

**Alternatives considérées**:
- MAUI XAML pur : Courbe d'apprentissage, pas de réutilisation
- Avalonia : Support Linux mais pas de Blazor Hybrid

### D5: Copier les composants UI plutôt que les partager

**Rationale**:
- L'UI mobile sera significativement différente (pas d'actions de modification, layout adapté)
- Évite la complexité des conditionnels dans les composants
- Permet une évolution indépendante

### D6: Configuration du chemin .db via settings MAUI

L'app MAUI aura un écran de configuration pour :
- Sélectionner le fichier .db (file picker)
- Mémoriser le chemin dans les préférences de l'app

**Rationale**: Simple, pas de magie. L'utilisateur sait où est son fichier synchronisé.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Duplication des composants UI | Acceptable pour la simplicité, documenter les différences |
| .db verrouillé pendant sync | SQLite supporte les lectures concurrentes, mode WAL |
| MAUI pas stable sur toutes plateformes | Commencer par Android, valider iOS ensuite |
| Visual Studio 2026 requis pour MAUI net10 | Vérifier version VS installée |

## Migration Plan

**Phase 1: Extraction Core + Desktop**
1. Créer FileIndexer.Core, déplacer Models/ et Data/
2. Déplacer SearchService et CollectionService (read methods)
3. Créer FileIndexer.Desktop, déplacer services restants
4. Adapter FileIndexer.Web pour référencer les deux
5. Valider que l'app Web fonctionne identiquement

**Phase 2: Création MAUI**
1. Créer FileIndexer.Maui (template Blazor Hybrid)
2. Référencer Core
3. Copier les composants UI essentiels (SearchView adapté)
4. Implémenter l'écran de configuration .db
5. Tester sur Android émulateur

**Phase 3: Polish**
1. Adapter l'UI pour mobile (responsive, touch-friendly)
2. Tester sur device réel
3. Builds iOS si applicable
