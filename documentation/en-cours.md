# En-cours — travaux restants

Suivi de la dette technique et des améliorations identifiées lors de l'audit (2026-06-19).
Les éléments **faits** sont en bas pour mémoire.

## 🔴 Priorité haute

### Couverture de tests à étendre
Le projet de tests existe (`tests/FileIndexer.Tests`) mais ne couvre que `IndexDbContext`,
`BuildFtsQuery`, `ArchiveService` et `FileOperationsService`. Manquent :
- Services corbeille (`WindowsTrashService`, `LinuxTrashService`, `MacTrashService`).
- `FileScannerService` (scan parallèle, scan incrémental, exclusions).
- `SearchService` (au-delà du build de requête FTS).

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

### #7 — Logging absent (reste partiel)
- ✅ `FileOperationsService` : agrégation succès/échecs/skip + `ILogger` injecté, le lot ne
  s'arrête plus au premier échec (couvert par 10 tests). Voir Fait.
- ⏳ Reste : aucun `ILogger` dans `ArchiveService` (extraction destructrice non tracée) ;
  `MauiFolderPickerService` utilise `Debug.WriteLine` ad-hoc.

## 🟡 Mineur

- **Chemin en dur** `R:\JDR` dans `src/FileIndexer.Web/appsettings.json` (et recopié dans
  `publish/win/appsettings.json`) → externaliser / valeur par défaut neutre.
- **Chemin DB relatif** `fileindex.db` non normalisé (`Path.GetFullPath`) → dépend du
  working directory selon le point de lancement.
- **Garde anti-path-traversal** de `ArchiveService` imparfait : `StartsWith(fullExtractDir)`
  sans séparateur final (faux positif `extract` vs `extract2`) et l'écriture réelle
  (`entry.WriteToDirectory(extractDir, ExtractFullPath:true)`) refait sa propre résolution.
  → valider via le séparateur et/ou écrire vers le chemin déjà validé.
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
- **#7 (partiel)** `FileOperationsService` : `OperationResult` étendu (SuccessCount /
  SkippedCount / Errors), copy/move/delete continuent sur erreur et agrègent, `ILogger`
  injecté (logs en anglais). 10 tests ajoutés. Reste : logging `ArchiveService` /
  `MauiFolderPickerService`.
- **#8** Dépendance vulnérable (NU1903) : `SQLitePCLRaw.bundle_e_sqlite3` épinglé en 3.0.3
  pour écraser le transitif 2.1.11 vulnérable. (`FileIndexer.Core.csproj`)
- **CI** Build cassé (version vide → MSB4044) corrigé + warnings MAUI (CS0649, CS0618,
  CA1416) et action `gh-release` v2. CI verte, 0 warning code.
- Bonus : `id IN (...)` paramétré (Dapper) ; bug env `DOTNET_ENVIRONMENT` /
  `ASPNETCORE_ENVIRONMENT` au démarrage MAUI ; progression détaillée du journal d'activité
  (X/total + élément en cours) ; rafraîchissement par collection pendant le reindex-all.
