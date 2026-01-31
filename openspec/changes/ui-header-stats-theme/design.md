## Context

L'application utilise actuellement un thème sombre avec des variables CSS dans `:root`. Le layout a un footer pour les stats et un header séparé pour la recherche.

## Goals / Non-Goals

**Goals:**
- Header unifié avec titre, recherche, boutons et stats
- Toggle thème jour/nuit avec persistance localStorage
- Liste pleine largeur

**Non-Goals:**
- Détection automatique du thème système (prefers-color-scheme)
- Plus de 2 thèmes

## Decisions

### 1. Gestion du thème via classe CSS sur body

**Choix** : Ajouter une classe `light-theme` sur `<body>` pour le mode jour. Le mode nuit reste le défaut (pas de classe).

**Rationale** : Simple à implémenter, pas besoin de dupliquer toutes les variables. On override seulement les variables nécessaires.

```css
:root { /* thème sombre par défaut */ }
body.light-theme { /* override pour mode jour */ }
```

### 2. Persistance via localStorage

**Choix** : Stocker le thème dans `localStorage.theme` ("light" ou "dark").

**Rationale** : Simple, pas besoin de backend. Le script s'exécute avant le rendu pour éviter le flash.

### 3. Script inline dans le head

**Choix** : Ajouter un script inline dans `<head>` pour appliquer le thème avant le rendu.

**Rationale** : Évite le "flash" de changement de thème au chargement.

### 4. Layout header sur deux lignes

**Choix** :
- Ligne 1: Titre + Recherche + Boutons (indexation, thème)
- Ligne 2: Stats (fichiers affichés, total index, taille, dernière indexation)

**Rationale** : Garde toutes les infos importantes visibles sans scroll.

## Risks / Trade-offs

**[Flash au premier chargement]** → Script inline dans head pour mitiger.

**[JS désactivé]** → Thème sombre par défaut, pas de toggle. Acceptable.
