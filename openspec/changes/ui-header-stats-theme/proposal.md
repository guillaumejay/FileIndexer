## Why

Optimiser l'espace vertical en déplaçant les stats dans le header et permettre à l'utilisateur de choisir entre un thème clair et sombre.

## What Changes

- **Stats en header** : Les statistiques (nombre de fichiers, taille DB, dernière indexation) passent du footer au header, sous le titre
- **Barre de titre unifiée** : Le titre de l'application, la recherche et le bouton d'indexation sont sur la même ligne
- **Liste pleine largeur** : Suppression de toute contrainte `max-width` sur la liste
- **Toggle thème** : Bouton pour basculer entre mode jour et mode nuit, avec persistance du choix

## Capabilities

### New Capabilities

- `theme-toggle`: Basculement entre thème clair et sombre avec persistance localStorage

### Modified Capabilities

(Aucune - modifications CSS/layout uniquement)

## Impact

- `Components/Pages/Home.razor` : Réorganisation du layout header
- `wwwroot/css/app.css` : Variables CSS pour thème clair, suppression max-width, nouveau layout header
