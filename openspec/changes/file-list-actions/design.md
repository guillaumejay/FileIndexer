## Context

L'application FileIndexer affiche une liste virtualisée de fichiers indexés. Actuellement, la seule interaction possible est de copier le chemin au clic. Les utilisateurs ont besoin d'effectuer des opérations fichiers directement depuis l'interface.

L'application est principalement utilisée localement (indexeur NAS), ce qui permet d'utiliser `Process.Start` pour les opérations OS et les dialogues natifs.

## Goals / Non-Goals

**Goals:**
- Permettre d'ouvrir un fichier ou son dossier parent en double-cliquant
- Offrir un menu contextuel avec Renommer, Copier, Déplacer, Supprimer
- Supporter la multi-sélection pour les opérations groupées
- Utiliser les dialogues natifs OS quand disponibles
- Maintenir la cohérence entre le système de fichiers et la base de données

**Non-Goals:**
- Support du glisser-déposer (drag & drop)
- Suppression définitive (toujours via corbeille)
- Prévisualisation de fichiers
- Opérations asynchrones avec barre de progression (les fichiers sont locaux, donc rapides)

## Decisions

### 1. Architecture du service d'opérations fichiers

**Décision** : Créer un `FileOperationsService` qui encapsule toutes les opérations OS et la synchronisation DB.

**Alternatives considérées** :
- Mettre la logique directement dans les composants Razor → Couplage fort, difficile à tester
- Séparer en deux services (OS + DB) → Complexité accrue pour garantir l'atomicité

**Rationale** : Un seul service garantit que la DB n'est mise à jour que si l'opération OS réussit.

```
FileOperationsService
├── OpenFileAsync(path)           → Process.Start
├── OpenFolderAsync(path)         → explorer.exe /select
├── RenameFileAsync(id, newName)  → File.Move + DB update
├── CopyFilesAsync(ids, dest)     → File.Copy + DB insert
├── MoveFilesAsync(ids, dest)     → File.Move + DB update
└── DeleteFilesAsync(ids)         → ITrashService + DB delete
```

### 2. Sélecteur de dossier cross-platform

**Décision** : Utiliser `CommonOpenFileDialog` sur Windows avec fallback sur `FolderBrowser` custom pour les autres OS.

**Alternatives considérées** :
- FolderBrowser custom partout → UX inférieure sur Windows
- Zenity/osascript sur Linux/macOS → Dépendances externes non garanties

**Rationale** : Meilleure UX sur Windows (cas principal) tout en restant fonctionnel partout.

```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    return await ShowWindowsFolderPickerAsync();
else
    return await ShowCustomFolderPickerAsync();
```

### 3. Gestion de la multi-sélection

**Décision** : Maintenir un `HashSet<long>` des IDs sélectionnés dans `SearchView.razor`, avec Ctrl+clic pour toggle et Shift+clic pour plage.

**Alternatives considérées** :
- Checkbox visible sur chaque ligne → Encombre l'interface
- Sélection stockée côté service → Problèmes de synchronisation avec la virtualisation

**Rationale** : Comportement familier (comme l'explorateur Windows), interface épurée.

### 4. Renommage inline

**Décision** : Remplacer la cellule du nom par un `<input>` quand le mode édition est actif.

**Déclencheurs** :
- Clic droit → Renommer dans le menu contextuel
- Touche F2 quand un fichier est sélectionné
- Double-clic lent sur le nom (clic, pause 500ms, clic - distinct du double-clic rapide qui ouvre)

**Workflow** :
1. Un des déclencheurs active le mode édition sur la ligne
2. Le nom actuel est pré-sélectionné (sans extension)
3. Entrée = confirmer, Échap = annuler, clic ailleurs = confirmer
4. Validation : nom non vide, pas de caractères interdits, pas de conflit

### 5. Gestion des conflits de nom

**Décision** : Dialogue modal avec trois options : Remplacer / Garder les deux / Annuler.

"Garder les deux" génère un nom avec suffixe : `fichier (1).ext`, `fichier (2).ext`, etc.

### 6. Suppression vers la corbeille cross-platform

**Décision** : Créer un `ITrashService` avec implémentations spécifiques par OS.

| OS | Implémentation |
|----|----------------|
| Windows | `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile()` avec `RecycleOption.SendToRecycleBin` |
| Linux | Appel externe à `trash-put` (trash-cli) |
| macOS | Appel externe à `osascript -e 'tell app "Finder" to delete POSIX file "..."'` |

**Comportement si outil non disponible** : Bloquer l'opération avec message explicite ("trash-cli n'est pas installé").

**Alternatives considérées** :
- Suppression définitive avec confirmation → Trop risqué, pas de récupération possible
- Suppression définitive si corbeille indisponible → Incohérent, surprenant pour l'utilisateur

**Rationale** : La corbeille est un filet de sécurité essentiel. Mieux vaut bloquer que supprimer définitivement par accident.

### 7. Stockage des fichiers sélectionnés avec virtualisation

**Décision** : Stocker les IDs (pas les objets) car la virtualisation ne garde pas tous les objets en mémoire.

Quand une opération est déclenchée, on récupère les données complètes depuis la DB via `GetFilesByIdsAsync(ids)`.

## Risks / Trade-offs

**[Process.Start sur serveur distant]** → L'application est conçue pour un usage local. Si déployée sur un serveur, les opérations d'ouverture de fichiers échoueront silencieusement. Mitigation : documenter cette limitation.

**[Fichier verrouillé pendant opération]** → L'opération OS échouera. Mitigation : afficher un dialogue d'erreur explicite avec le message système.

**[Désynchronisation DB si crash mid-opération]** → Rare car opérations rapides sur fichiers locaux. Mitigation : faire l'opération OS d'abord, DB ensuite. Au pire, la DB contient une entrée obsolète plutôt qu'une entrée fantôme.

**[Performance multi-sélection massive]** → Copier 10000 fichiers pourrait bloquer l'UI. Mitigation pour v1 : pas de gestion, acceptable car usage principal = quelques fichiers. Amélioration future possible avec background worker.

**[Dialogue natif Windows bloquant]** → Le dialogue est modal sur le bureau, pas dans le navigateur. Peut surprendre l'utilisateur. Mitigation : acceptable pour un outil local, mentionner dans la doc si nécessaire.

**[trash-cli non installé sur Linux]** → La suppression sera bloquée. Mitigation : message d'erreur explicite avec instructions d'installation ("Installez trash-cli : sudo apt install trash-cli").
