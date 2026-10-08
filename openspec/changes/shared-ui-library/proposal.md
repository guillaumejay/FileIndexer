## Why

`maui-architecture-refactor` a choisi de copier les composants UI dans MAUI plutôt que de les partager. Une revue (oct. 2026) a mesuré ~1 400 lignes de Razor/C# et ~870 lignes de CSS dupliquées (80 à 96 % identiques), et la divergence a produit des bugs : renommage et dialogue de conflit absents de MAUI (qui choisissait toujours « garder les deux »), sélection Shift fausse côté MAUI, « Copier le chemin » perdu côté Web, gestion d'erreurs présente d'un seul côté. Chaque correctif devait être fait deux fois.

## What Changes

- **Nouveau projet FileIndexer.UI** (Razor Class Library, net10.0) : tous les composants (AppShell, SearchView, CollectionsView, CollectionEditor, ActivityIndicator, FolderBrowser, Dialogs, Modal, Toast), le CSS et le JS partagés.
- **Web et MAUI deviennent des hôtes minces** : configuration, une page, services natifs.
- **Différences de plateforme au runtime** au lieu de `#if DESKTOP` dans les composants : `PlatformCapabilities` (accès au système de fichiers, tactile) et services hôtes optionnels (`INativeFolderPicker`, `IConfigFileExchange`).
- **MAUI** : changement de base sans redémarrage (`IndexDbContext.SwitchTo`) ; sur Android/iOS la base est copiée dans le stockage de l'app (le chemin du sélecteur est un cache temporaire).
- **Mobile en lecture seule** : édition/indexation des collections masquées sans accès au système de fichiers.
- **Web** : n'accepte que `localhost` par défaut (`AllowedHosts`), réglages de scan appliqués, page `/Error` ajoutée.

## Capabilities

### Modified Capabilities
- `multi-project-structure` : cinq projets ; Desktop est désormais référencé par toutes les cibles via UI, ses services n'étant enregistrés que si l'hôte a accès au système de fichiers.

## Impact

- **Structure solution** : Core, Desktop, UI, Web, Maui (+ tests).
- **Dépendances** : UI → Core + Desktop ; Web → UI ; Maui → UI (donc Desktop aussi sur Android/iOS, inutilisé à l'exécution).
- **Fichiers supprimés** : `Web/Components/Pages/*`, `Maui/Components/Pages/*` (remplacés), `Web/Services/*` (inutilisés), `wwwroot/css/app.css` des deux hôtes.
- **Comportement** : thème stocké dans `localStorage` pour les deux hôtes (MAUI utilisait `Preferences`), libellés UI unifiés en anglais.
