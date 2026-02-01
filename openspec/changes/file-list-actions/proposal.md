## Why

L'interface actuelle de la liste de fichiers ne permet que de copier le chemin au clic. Les utilisateurs ont besoin d'interagir directement avec les fichiers depuis l'application : ouvrir, renommer, copier et déplacer, sans quitter l'interface.

## What Changes

- Double-clic sur le nom du fichier ouvre le fichier avec l'application par défaut
- Double-clic sur le dossier ouvre l'explorateur de fichiers à cet emplacement
- Menu contextuel (clic droit) avec options : Renommer, Copier, Déplacer, Supprimer
- Renommage inline directement dans le tableau (Entrée = confirmer, Échap = annuler)
- Multi-sélection via Ctrl+clic et Shift+clic pour les opérations Copier/Déplacer/Supprimer
- Suppression vers la corbeille OS (Windows natif, trash-cli sur Linux, osascript sur macOS)
- Sélecteur de dossier natif Windows avec fallback sur FolderBrowser custom pour Linux/macOS
- Dialogue de confirmation en cas de conflit de nom (Remplacer / Garder les deux / Annuler)
- Synchronisation de la base de données uniquement après succès confirmé des opérations OS

## Capabilities

### New Capabilities

- `file-actions`: Actions sur les fichiers (ouvrir fichier, ouvrir dossier parent)
- `file-operations`: Opérations fichiers avec synchronisation DB (renommer, copier, déplacer, supprimer)
- `trash-service`: Service de suppression vers corbeille cross-platform
- `context-menu`: Menu contextuel sur les éléments de la liste
- `multi-selection`: Sélection multiple via Ctrl+clic et Shift+clic
- `native-folder-picker`: Sélecteur de dossier natif Windows avec fallback cross-platform

### Modified Capabilities

- `virtualized-file-list`: Ajout des gestionnaires d'événements (double-clic, clic droit, sélection)

## Impact

- **Services** : Nouveau `FileOperationsService` pour les opérations OS et synchronisation DB
- **Data** : Nouvelles méthodes dans `IndexDbContext` (UpdateFilePath, InsertSingleFile, DeleteFile)
- **UI** : Modifications de `SearchView.razor` (événements, état de sélection, modals)
- **Composants** : Nouveaux composants `ContextMenu.razor`, `ConflictDialog.razor`, `ErrorDialog.razor`
- **Dépendances** : Ajout de `Microsoft.WindowsAPICodePack-Shell` pour le dialogue natif Windows
