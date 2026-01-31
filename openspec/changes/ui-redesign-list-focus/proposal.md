## Why

L'interface actuelle divise l'écran en sections empilées (indexation, stats, recherche) alors que l'usage principal est la consultation et recherche de fichiers. La liste des résultats doit être l'élément central et occuper tout l'espace disponible.

## What Changes

- **Refonte du layout** : La liste de fichiers devient l'élément principal occupant tout l'écran
- **Barre de recherche** : Déplacée en haut de l'écran avec un bouton d'accès aux paramètres
- **Modal d'indexation** : Les contrôles de scan sont déplacés dans une modal centrée (accessible via bouton settings)
- **Footer de statut** : Les statistiques sont condensées dans une barre de statut en bas (nombre de fichiers affichés, total indexé, taille DB)
- **Virtualisation** : La liste utilise `<Virtualize>` avec `ItemsProvider` pour supporter des millions de fichiers
- **Tri par colonnes** : Headers cliquables pour trier par nom, répertoire, extension, taille, date (défaut: nom A-Z)
- **Nouveaux index SQLite** : Ajout d'index sur `name` et `size_bytes` pour les tris

## Capabilities

### New Capabilities

- `virtualized-file-list`: Liste de fichiers virtualisée avec tri par colonnes et chargement à la demande
- `indexation-modal`: Modal centrée pour les contrôles d'indexation et la progression du scan
- `status-bar`: Barre de statut en footer affichant les statistiques de l'index et le nombre de résultats

### Modified Capabilities

(Aucune spec existante à modifier)

## Impact

- `Components/Pages/Home.razor` : Refonte complète du composant
- `Data/IndexDbContext.cs` : Nouvelle méthode de recherche avec tri paramétrable, ajout d'index
- `Services/SearchService.cs` : Support du tri dans les requêtes
- `wwwroot/app.css` : Nouveaux styles pour layout full-height, modal, footer
