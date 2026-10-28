## 1. Core et scanner

- [x] 1.1 `SearchWithSortAsync(underPath)` et `CountFilesUnderAsync`
- [x] 1.2 `BuildFtsQuery` : alternatives `a|b`, variantes `D&D` / `DnD` / `D and D`
- [x] 1.3 Classement bm25 pondéré par colonne
- [x] 1.4 `ScanCollectionAsync(subtree)` et `GetCollectionFileStampsAsync(underPath)`

## 2. Serveur MCP

- [x] 2.1 Projet FileIndexer.Mcp (stdio, logs sur stderr, `--db` obligatoire)
- [x] 2.2 `IndexPaths` : rattachement à la collection et casse du disque
- [x] 2.3 Outils `search_files`, `search_dirs`, `list_dir`, `rescan`, `index_status`
- [x] 2.4 `ScanJobs` : un scan à la fois, en arrière-plan
- [x] 2.5 Outils `move`, `rename`, `delete` (corbeille), résultat par élément, indexation à la volée d'un fichier inconnu
- [x] 2.6 `MovePathAsync` : rattachement à la collection de destination
- [x] 2.7 Journal des opérations en base (table `operations`, `IndexOperationJournal`), comptages dans `index_status`
- [x] 2.8 Journal enregistré aussi par les hôtes Web et MAUI (`AddFileIndexer`)

## 3. Vérification et docs

- [x] 3.1 Tests : requêtes, filtre sous-dossier, comptage, rescan de sous-dossier, canonisation
- [x] 3.2 Essai stdio sur une copie de la base réelle (rescan complet de R:\JDR : 88 s, 159 370 entrées)
- [x] 3.3 README (installation, `claude mcp add`)
- [ ] 3.4 Utilisation réelle par un agent (session de rangement JdR)
