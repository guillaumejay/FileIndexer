## Context

L'application FileIndexer affiche actuellement une interface en sections empilées (indexation, stats, recherche). Le composant `Home.razor` gère tout dans un seul fichier avec pagination manuelle (100 éléments par page). La recherche utilise SQLite FTS5 et les résultats sont triés par date de modification descendante par défaut.

Contraintes :
- L'index peut contenir des millions de fichiers (NAS)
- Blazor Server avec connexion SignalR persistante
- SQLite single-connection (singleton `IndexDbContext`)

## Goals / Non-Goals

**Goals:**
- Layout full-height avec liste occupant l'espace disponible
- Virtualisation pour afficher des millions de fichiers sans pagination
- Tri par colonnes avec requêtes SQL optimisées
- Modal pour isoler les contrôles d'indexation
- Footer compact avec stats essentielles

**Non-Goals:**
- Opérations sur les fichiers (move, rename, delete) - reporté
- Multi-sélection de fichiers
- Filtres avancés (par date, taille, etc.)
- Changement de thème

## Decisions

### 1. Virtualisation avec ItemsProvider

**Choix** : Utiliser `<Virtualize ItemsProvider="...">` plutôt que `Items="collection"`.

**Alternatives considérées** :
- `Items` avec collection complète : Charge tout en mémoire (~200 bytes × 1M fichiers = 200MB RAM)
- `ItemsProvider` : Requête SQL à la demande, mémoire constante

**Rationale** : Pour un NAS avec potentiellement des millions de fichiers, le chargement à la demande est nécessaire. Le léger délai au scroll rapide est acceptable.

### 2. Tri côté SQL

**Choix** : Le tri est effectué côté SQL avec `ORDER BY` dynamique.

**Alternatives considérées** :
- Tri côté client après chargement : Impossible avec ItemsProvider (données partielles)
- Tri côté serveur en mémoire : Nécessiterait de tout charger

**Rationale** : Seule option viable avec la virtualisation. Nécessite des index SQLite pour la performance.

### 3. Nouveaux index SQLite

**Choix** : Ajouter `idx_files_name` et `idx_files_size`.

Index existants : `extension`, `directory`, `modified_at_utc`
Index à ajouter : `name`, `size_bytes`

**Rationale** : Sans index, un `ORDER BY name` sur 1M lignes serait lent. Les index permettent un tri quasi-instantané.

### 4. Structure du composant

**Choix** : Garder un seul composant `Home.razor` avec la modal inline.

**Alternatives considérées** :
- Extraire des sous-composants (`FileList.razor`, `IndexationModal.razor`, `StatusBar.razor`)
- Garder monolithique

**Rationale** : La complexité actuelle ne justifie pas l'extraction. L'état est partagé (stats, progression scan, résultats). On pourra extraire plus tard si le fichier devient trop gros.

### 5. Gestion du tri avec recherche FTS

**Choix** : Quand une recherche FTS est active, le tri par défaut reste le rank FTS. L'utilisateur peut choisir un autre tri.

**Alternatives considérées** :
- Forcer le tri par rank toujours
- Ignorer le rank et utiliser le tri utilisateur

**Rationale** : Le rank FTS donne les résultats les plus pertinents en premier, mais l'utilisateur peut vouloir trier autrement (ex: fichiers les plus gros correspondant à "video").

## Risks / Trade-offs

**[Latence au scroll rapide]** → Acceptable pour l'instant. Si problématique, on pourra augmenter `OverscanCount` ou implémenter un cache local.

**[Requêtes COUNT(*) coûteuses]** → Le footer affiche le nombre total de résultats. Pour des tables volumineuses, `COUNT(*)` peut être lent. Mitigation : on utilise déjà cette valeur, pas de régression.

**[Changement de tri pendant le scroll]** → Appeler `RefreshDataAsync()` sur le composant `Virtualize` pour recharger. L'utilisateur perd sa position de scroll, ce qui est le comportement attendu.

**[Index SQLite augmentent la taille de la DB]** → Impact mineur (~10-20% de la taille de la table). Acceptable pour la performance.
