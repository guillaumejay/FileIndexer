## Why

Un agent IA qui range des fichiers (~470 PDF de JdR vers `R:\JDR`, ~149 000 fichiers, ~10 600 dossiers) doit savoir où sont déjà rangés le même jeu, la même gamme ou le même éditeur. Sans index, il lance `find` sur R: (plusieurs minutes) et rate des sous-dossiers. FileIndexer a déjà cet index, mais seulement derrière une UI.

## What Changes

- **Nouveau projet FileIndexer.Mcp** : serveur MCP (stdio) au-dessus d'une base FileIndexer existante (`--db`).
- **Outils** : `search_files`, `search_dirs`, `list_dir` (nombre de fichiers par sous-dossier), `rescan` (incrémental, limité à un dossier, en arrière-plan au-delà de 30 s), `move`, `rename`, `delete` (corbeille), `index_status`.
- **Journal des opérations en base** (table `operations`) : chaque déplacement, renommage, copie ou suppression fait par `FileOperationsService`, depuis le MCP, le Web ou MAUI, pour l'historique et le comptage (`index_status`).
- **Core** : filtre de recherche « sous ce dossier », comptage de fichiers par sous-arbre, alternatives `a|b`, équivalence `D&D` = `DnD` = `D and D`, classement bm25 qui pondère le nom plus que les dossiers (aussi pour l'UI).
- **Scanner** : rescan limité à un sous-dossier d'une collection, qui ne purge que ce sous-dossier.
- **Déplacement entre collections** (aussi pour l'UI) : un élément déplacé dans les dossiers d'une autre collection y est rattaché dans l'index ; hors de toute collection, il en sort.

## Non-goals

- Recherche dans le texte des PDF (rien n'est extrait aujourd'hui ; projet à part entière).
- Recherche floue (« Shadow Dark » ne trouve pas « Shadowdark » ; les préfixes, oui).
- Création ou modification de collections via MCP, export de la configuration (rare : il se fait depuis l'app ; un outil de plus coûterait du contexte à chaque session).
