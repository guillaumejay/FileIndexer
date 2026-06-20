# En-cours — travaux restants

Suivi de la dette technique et des améliorations identifiées lors de l'audit (2026-06-19).
Les éléments **faits** sont en bas pour mémoire.

## 🟠 Priorité moyenne

### #4 — Duplication massive Web / MAUI (~1700 lignes)
- Composants quasi identiques : `ActivityIndicator.razor`, `CollectionEditor.razor`,
  `CollectionsView.razor` (90-100 % dupliqués entre `FileIndexer.Web` et `FileIndexer.Maui`).
- Logique batch copiée entre `SearchView.razor` (Web) et `Home.razor` (MAUI) :
  extract / copy / move / delete / reindex.
- **Pistes :**
  - Extraire les composants partagés dans une **Razor Class Library** (RCL).
  - Descendre la logique batch dans un service partagé (`FileOperationsService` est déjà
    le bon endroit pour copy/move/delete ; prévoir un coordinateur pour extract/reindex).
  - Conséquence directe : éviter d'avoir à appliquer chaque correctif **en double**.

## 🟡 Mineur

- **Cohérence langue** : messages d'erreur en français dans les services alors que
  `agents.md` demande *"consistent English"*. Harmoniser (UI vs core).

## ✅ Fait (pour mémoire)

- **#1** Accès SQLite thread-safe : connexion par opération (pooling) + WAL + busy_timeout ;
  cas `:memory:` via shared-cache + keep-alive. (`IndexDbContext.cs`)
- **#2** Recherche : court-circuit quand la requête ne produit aucun token FTS (plus de
  `MATCH ''` invalide). (`IndexDbContext.cs`)
- **#3** Création du projet de tests xUnit (22 tests, dont stress de concurrence).
- **#5** Suppression de tous les `async void` côté MAUI → `async Task`.
- **#6** Migrations sans exception : vérification `pragma_table_info` avant `ALTER TABLE`.
- **#7** Échecs partiels + logging. `FileOperationsService` : `OperationResult` étendu
  (SuccessCount / SkippedCount / Errors), copy/move/delete continuent sur erreur et agrègent,
  `ILogger` injecté (10 tests). `ArchiveService` et `MauiFolderPickerService` : `ILogger`
  injecté (extraction tracée, skip path-traversal loggé ; plus de `Debug.WriteLine`). Logs en
  anglais, messages UI en FR.
- **#8** Dépendance vulnérable (NU1903) : `SQLitePCLRaw.bundle_e_sqlite3` épinglé en 3.0.3
  pour écraser le transitif 2.1.11 vulnérable. (`FileIndexer.Core.csproj`)
- **Path-traversal `ArchiveService`** : containment validé avec séparateur final (plus de
  faux positif `data` vs `data-evil`) + écriture vers le chemin validé (`WriteToFile`) au
  lieu de `WriteToDirectory` qui re-résolvait `entry.Key`. Test de régression ajouté.
- **Mineurs config Web** : `DefaultScanPath` `R:\JDR` en dur → valeur vide neutre ;
  chemin DB relatif normalisé via `Path.GetFullPath(..., ContentRootPath)` (indépendant
  du working directory). (`appsettings.json`, `Program.cs`)
- **Tests corbeille** : `WindowsTrashService` (chemin inexistant, fichier/dossier réels →
  corbeille, `IsSupported`), `MacTrashService.IsSupported`, branche Linux non-supporté.
  6 tests, intégration Windows gated `[WindowsOnlyFact]`.
- **Tests `FileScannerService`** : scan complet (sous-dossiers), non-incrémental qui purge,
  exclusion par nom de dossier, incrémental (skip inchangés + ajout nouveaux), garde
  sans-chemin, chemin inexistant. 6 tests.
- **Tests `SearchService`** : filtre collection, tri nom/taille, filtres extension/répertoire,
  toggle dossiers, `SearchByExtension` (normalisation), stats (comptes/tailles/extensions),
  pagination. 11 tests. **Couverture haute priorité complète — 56 tests au total.**
- **CI** Build cassé (version vide → MSB4044) corrigé + warnings MAUI (CS0649, CS0618,
  CA1416) et action `gh-release` v2. CI verte, 0 warning code.
- Bonus : `id IN (...)` paramétré (Dapper) ; bug env `DOTNET_ENVIRONMENT` /
  `ASPNETCORE_ENVIRONMENT` au démarrage MAUI ; progression détaillée du journal d'activité
  (X/total + élément en cours) ; rafraîchissement par collection pendant le reindex-all.
