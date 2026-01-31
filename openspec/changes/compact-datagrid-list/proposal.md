## Why

L'affichage actuel de la liste de fichiers utilise un style moderne avec beaucoup d'espacement (lignes de 36px, padding généreux). Pour une application d'indexation de fichiers destinée à afficher des milliers de résultats, un style plus compact et strict type "DataGrid Windows" permet de voir plus de fichiers à l'écran et facilite le scan visuel rapide.

## What Changes

- Réduire la hauteur des lignes de 36px à ~20-22px pour une densité maximale
- Ajouter des bordures de grille visibles entre toutes les cellules (style DataGrid)
- Implémenter le zebra striping (couleurs de fond alternées) pour faciliter le suivi horizontal
- Ajuster les en-têtes de colonnes pour un style plus strict et classique
- Réduire le padding dans les cellules au minimum fonctionnel
- Utiliser une police monospace ou condensée pour les données
- Tronquer les chemins longs à droite (afficher le début avec `...` à la fin) au lieu de la gauche
- Afficher le chemin complet en tooltip sur la colonne répertoire quand le chemin est tronqué

## Capabilities

### New Capabilities
- `compact-datagrid-style`: Style CSS de liste compacte type DataGrid Windows avec grille visible, zebra striping, et densité maximale
- `path-tooltip`: Tronquer le chemin à droite (afficher le début) avec `...` à la fin, et tooltip affichant le chemin complet quand tronqué

### Modified Capabilities
<!-- Aucune modification de specs existantes - changement purement visuel/CSS -->

## Impact

- `wwwroot/css/app.css`: Modification des styles `.file-list-table` et classes associées
- `Components/Pages/Home.razor`: Ajustement de `ItemSize` dans le composant Virtualize (36 → ~22), modification de `TruncatePath` pour tronquer à droite, ajout du tooltip sur la cellule répertoire
- Variables CSS: Possibles nouvelles variables pour les couleurs de zebra striping
