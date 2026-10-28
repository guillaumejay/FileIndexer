## Context

L'index (SQLite FTS5, tokenizer `unicode61 remove_diacritics 2`) ignore déjà la casse, les accents et la ponctuation (`_` compris). Les chemins sont comparés octet par octet en SQL (index `idx_files_path`, filtre par plage).

## Decisions

### D1 : serveur stdio séparé, même base
Le serveur ouvre la base de l'UI (`--db`, obligatoire et existante, pour ne pas créer un index vide par erreur). WAL permet la lecture pendant que l'UI ou un rescan écrit. Il est publié hors du dépôt pour qu'un rebuild ne verrouille pas l'exécutable en cours.

### D2 : chemins canonisés avant toute requête
`PathHelper.WithDiskCase` (Core), appelé par le serveur et par le scanner pour `subtree`. Un chemin tapé (`r:/jdr/d&d and clones`) est rattaché à la racine de collection la plus longue qui le contient, puis chaque segment prend la casse du disque. C'est ce que le scanner enregistre (`FullName` construit depuis la racine configurée) : sans cela, le filtre SQL ne trouverait rien et un rescan créerait des doublons de casse.

### D3 : rescan limité à un sous-dossier
`ScanCollectionAsync(..., subtree)` ne parcourt que ce dossier et ne charge que les entrées de l'index situées dessous : la purge ne touche pas le reste de la collection. Un seul scan à la fois ; l'outil attend 30 s puis rend la main, `index_status` suit la fin.

### D4 : variantes de nom dans le constructeur de requête
`a|b` donne `(A OR B)`. `x&y` ajoute `xny` et `x and y` ; `xnx` (deux lettres identiques, ex. `DnD`) ajoute `x&x` et `x and x`. Le cas inverse est limité aux lettres identiques pour que `and` ou `ant` restent des mots ordinaires.

### D5 : classement
`rank MATCH 'bm25(10, 2, 1)'` (nom, chemin, dossier). `bm25()` en ORDER BY n'est pas utilisable une fois les résultats groupés (dédoublonnage multi-collections) ; `rank` l'est.

### D6 : opérations sur fichiers dans le MCP
Ranger, renommer et supprimer font partie de l'usage normal : les faire par l'outil garde l'index juste sans rescan. `delete` passe par la corbeille. Un fichier absent de l'index mais présent sur le disque (téléchargement récent) est indexé à la volée ; un dossier non indexé demande un rescan. `ifExists` : `skip` par défaut, `keep_both`, `replace`.

### D7 : collection suivie au déplacement
`MovePathAsync` rattache les lignes déplacées à la collection dont un dossier contient la destination (racine la plus longue), ou les retire si aucune ne la contient. Une collection sans dossier garde ses lignes.

### D8 : journal en base
Table `operations` (date UTC, opération, chemin, destination, erreur, programme source), indexée sur la date, sans clé étrangère : les entrées survivent aux fichiers et collections qu'elles citent. `IFileOperationJournal` (Desktop), optionnel dans `FileOperationsService`, écrit une ligne par élément ; `IndexOperationJournal` l'enregistre dans la base et n'échoue jamais l'opération. Les trois hôtes l'enregistrent. Une table plutôt qu'un fichier : les comptages (`GetOperationCountsAsync`, affichés par `index_status`) sont une requête. Un chemin introuvable est rejeté avant le service et n'est pas journalisé.

## Risks

- Une base créée avant l'indexation des dossiers n'a aucune ligne dossier : `search_dirs` et `list_dir` sont vides jusqu'au premier rescan (`index_status` donne la date du dernier changement).
- `rescan` écrit dans la base partagée avec l'UI ; deux scans simultanés (UI + MCP) restent possibles car le verrou est par processus.
- **Suivi** : la vraie cause est l'identité de chemin octet par octet en SQL alors que `PathHelper.Comparison` ignore la casse sur Windows/macOS. Une colonne clé normalisée (indexée) supprimerait toute canonisation ; c'est une migration, hors de ce changement.
