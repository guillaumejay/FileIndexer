## Context

L'index (SQLite FTS5, tokenizer `unicode61 remove_diacritics 2`) ignore déjà la casse, les accents et la ponctuation (`_` compris). Les chemins sont comparés octet par octet en SQL (index `idx_files_path`, filtre par plage).

## Decisions

### D1 : serveur stdio séparé, même base
Le serveur ouvre la base de l'UI (`--db`, obligatoire et existante, pour ne pas créer un index vide par erreur). WAL permet la lecture pendant que l'UI ou un rescan écrit. Il est publié hors du dépôt pour qu'un rebuild ne verrouille pas l'exécutable en cours.

### D2 : chemins canonisés avant toute requête
Un chemin tapé (`r:/jdr/d&d and clones`) est rattaché à la racine de collection la plus longue qui le contient, puis chaque segment prend la casse du disque. C'est ce que le scanner enregistre (`FullName` construit depuis la racine configurée) : sans cela, le filtre SQL ne trouverait rien et un rescan créerait des doublons de casse.

### D3 : rescan limité à un sous-dossier
`ScanCollectionAsync(..., subtree)` ne parcourt que ce dossier et ne charge que les entrées de l'index situées dessous : la purge ne touche pas le reste de la collection. Un seul scan à la fois ; l'outil attend 30 s puis rend la main, `index_status` suit la fin.

### D4 : variantes de nom dans le constructeur de requête
`a|b` donne `(A OR B)`. `x&y` ajoute `xny` et `x and y` ; `xnx` (deux lettres identiques, ex. `DnD`) ajoute `x&x` et `x and x`. Le cas inverse est limité aux lettres identiques pour que `and` ou `ant` restent des mots ordinaires.

### D5 : classement
`rank MATCH 'bm25(10, 2, 1)'` (nom, chemin, dossier). `bm25()` en ORDER BY n'est pas utilisable une fois les résultats groupés (dédoublonnage multi-collections) ; `rank` l'est.

## Risks

- Une base créée avant l'indexation des dossiers n'a aucune ligne dossier : `search_dirs` et `list_dir` sont vides jusqu'au premier rescan (`index_status` donne la date du dernier changement).
- `rescan` écrit dans la base partagée avec l'UI ; deux scans simultanés (UI + MCP) restent possibles car le verrou est par processus.
