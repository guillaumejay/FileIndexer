## 1. CSS Variables

- [x] 1.1 Ajouter la variable `--row-alt` pour la couleur de fond alternée dans `:root` (thème sombre)
- [x] 1.2 Ajouter la variable `--row-alt` dans `body.light-theme` (thème clair)

## 2. Compact Row Style

- [x] 2.1 Modifier `.file-list-table td` padding de `0.5rem 1rem` à `2px 8px`
- [x] 2.2 Modifier `.file-list-table th` padding pour correspondre au style compact

## 3. Grid Borders

- [x] 3.1 Ajouter `border-right: 1px solid var(--border)` sur `.file-list-table td`
- [x] 3.2 Ajouter `border-right: 1px solid var(--border)` sur `.file-list-table th`
- [x] 3.3 Retirer le `border-right` de la dernière colonne (`:last-child`)

## 4. Zebra Striping

- [x] 4.1 Ajouter la règle `.file-list-table tbody tr:nth-child(even)` avec `background: var(--row-alt)`

## 5. Monospace Font

- [x] 5.1 Appliquer `font-family: monospace` sur `.file-list-table td`
- [x] 5.2 Ajuster `font-size` si nécessaire pour la lisibilité avec la police monospace

## 6. Virtualize ItemSize

- [x] 6.1 Modifier `ItemSize="36"` en `ItemSize="22"` dans le composant Virtualize de Home.razor

## 7. Path Truncation

- [x] 7.1 Modifier la méthode `TruncatePath` pour tronquer à droite: `path[..(maxLength - 3)] + "..."`

## 8. Path Tooltip

- [x] 8.1 Ajouter l'attribut `title="@context.Directory"` sur la cellule `<td class="file-dir">`
