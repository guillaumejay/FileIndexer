## Context

L'application FileIndexer affiche une liste de fichiers via un composant `<Virtualize>` avec une table HTML. Le style actuel est moderne avec des lignes de 36px et du padding généreux. L'utilisateur souhaite un style plus compact type "DataGrid Windows" pour maximiser le nombre de fichiers visibles.

Fichiers concernés:
- `wwwroot/css/app.css`: Styles de `.file-list-table`
- `Components/Pages/Home.razor`: Composant Virtualize et méthode `TruncatePath`

## Goals / Non-Goals

**Goals:**
- Maximiser la densité d'affichage (lignes ~20-22px)
- Style DataGrid classique avec grille visible et zebra striping
- Améliorer la lisibilité des chemins tronqués (troncature à droite + tooltip)

**Non-Goals:**
- Modifier la structure des colonnes existantes
- Changer les fonctionnalités de tri ou recherche
- Ajouter de nouvelles colonnes ou informations

## Decisions

### 1. Hauteur des lignes: 22px

**Choix:** Passer de 36px à 22px pour `ItemSize` du Virtualize.

**Rationale:** 22px permet d'afficher ~64% de lignes en plus tout en restant lisible. Plus petit (18-20px) risque des problèmes sur écrans haute résolution.

**CSS:** `padding: 2px 8px` au lieu de `0.5rem 1rem`.

### 2. Bordures de grille visibles

**Choix:** Ajouter `border-right` sur chaque `td` et `th`.

**Rationale:** Style DataGrid classique. Utiliser la variable `--border` existante pour cohérence avec les thèmes.

### 3. Zebra striping via CSS

**Choix:** Utiliser `tbody tr:nth-child(even)` avec une couleur de fond légèrement différente.

**Rationale:** Pur CSS, pas de logique côté serveur. Fonctionne avec Virtualize car les lignes DOM alternent naturellement.

**Variables:** Ajouter `--row-alt` pour la couleur alternée (légère variation de `--bg-primary`).

### 4. Troncature à droite des chemins

**Choix:** Modifier `TruncatePath` pour afficher le début du chemin avec `...` à la fin.

**Avant:** `"..." + path[^(maxLength - 3)..]` → `...ments\projets\2024`
**Après:** `path[..(maxLength - 3)] + "..."` → `\\serveur\partage\doc...`

**Rationale:** Le début du chemin (serveur, racine) est souvent plus informatif que la fin.

### 5. Tooltip natif HTML

**Choix:** Utiliser l'attribut `title` sur la cellule `<td>` du répertoire.

**Rationale:** Simple, natif, pas de dépendance JS. Le tooltip n'apparaît que si on survole, donc pas de surcharge visuelle.

**Implémentation:** `<td class="file-dir" title="@context.Directory">@TruncatePath(...)</td>`

## Risks / Trade-offs

**[Virtualize et zebra striping]** → Le zebra striping CSS avec `:nth-child` fonctionne sur les éléments DOM, pas sur les indices de données. Avec la virtualisation, quand l'utilisateur scroll, les lignes peuvent "sauter" de couleur.
→ *Mitigation:* Accepter ce comportement mineur. Alternative coûteuse: passer l'index en paramètre et utiliser une classe conditionnelle.

**[Densité élevée sur petits écrans]** → 22px peut être trop compact sur mobile.
→ *Mitigation:* Garder le style compact pour desktop. Les media queries existantes peuvent ajuster si nécessaire dans une future itération.

**[Tooltip sur chemin court]** → Le tooltip apparaîtra même si le chemin n'est pas tronqué.
→ *Mitigation:* Comportement acceptable - le tooltip confirme le chemin complet. Alternative: conditionner le `title` uniquement si tronqué (complexité mineure).
