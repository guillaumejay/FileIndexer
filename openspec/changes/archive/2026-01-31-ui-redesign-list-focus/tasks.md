## 1. Database Layer

- [x] 1.1 Ajouter les index SQLite `idx_files_name` et `idx_files_size` dans `InitializeDatabase()`
- [x] 1.2 Créer une méthode `SearchWithSortAsync()` dans `IndexDbContext` avec paramètres de tri (colonne, direction)
- [x] 1.3 Supporter le tri dynamique dans les requêtes FTS (ORDER BY rank ou colonne utilisateur)

## 2. Service Layer

- [x] 2.1 Créer un enum `SortColumn` (Name, Directory, Extension, Size, ModifiedAt, Rank)
- [x] 2.2 Créer un enum `SortDirection` (Asc, Desc)
- [x] 2.3 Ajouter une méthode `SearchAsync()` dans `SearchService` avec paramètres de tri

## 3. Layout et Structure CSS

- [x] 3.1 Créer le layout full-height (header recherche, liste flex-grow, footer fixe)
- [x] 3.2 Styles pour la barre de recherche avec bouton settings à droite
- [x] 3.3 Styles pour le footer de statut
- [x] 3.4 Styles pour la modal centrée avec overlay

## 4. Liste Virtualisée

- [x] 4.1 Remplacer la table paginée par `<Virtualize>` avec `ItemsProvider`
- [x] 4.2 Implémenter `ItemsProvider` appelant `SearchService.SearchAsync()`
- [x] 4.3 Ajouter les headers de colonnes cliquables avec indicateurs de tri (▲/▼)
- [x] 4.4 Gérer le changement de tri (toggle direction, changement de colonne)
- [x] 4.5 Appeler `RefreshDataAsync()` lors du changement de tri ou recherche

## 5. Modal d'Indexation

- [x] 5.1 Ajouter l'état `showModal` et le bouton settings dans la barre de recherche
- [x] 5.2 Créer le markup de la modal (overlay + contenu centré)
- [x] 5.3 Déplacer les contrôles d'indexation (chemin, checkbox incrémental, bouton) dans la modal
- [x] 5.4 Déplacer la barre de progression et les stats de scan dans la modal
- [x] 5.5 Implémenter la fermeture (bouton X, clic overlay, touche Escape)

## 6. Footer Status Bar

- [x] 6.1 Créer le markup du footer avec les 3 sections (résultats, total index, dernière indexation)
- [x] 6.2 Afficher le nombre de résultats actuels (mis à jour par l'ItemsProvider)
- [x] 6.3 Afficher les stats globales (total fichiers, taille DB)
- [x] 6.4 Formater la date de dernière indexation en relatif ("il y a 2h")

## 7. Cleanup

- [x] 7.1 Supprimer l'ancienne section stats avec les cards
- [x] 7.2 Supprimer l'ancienne pagination
- [x] 7.3 Supprimer les tags d'extension cliquables (ou les déplacer dans la modal si souhaité)
- [x] 7.4 Nettoyer les styles CSS inutilisés
