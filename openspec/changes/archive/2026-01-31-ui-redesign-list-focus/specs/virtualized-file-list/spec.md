## ADDED Requirements

### Requirement: Liste virtualisée affiche tous les fichiers
Le système SHALL afficher tous les fichiers indexés dans une liste virtualisée qui occupe tout l'espace vertical disponible entre la barre de recherche et le footer.

#### Scenario: Chargement initial
- **WHEN** l'utilisateur accède à la page d'accueil
- **THEN** la liste affiche tous les fichiers triés par nom (A-Z)
- **THEN** seuls les éléments visibles sont rendus dans le DOM

#### Scenario: Scroll dans une grande liste
- **WHEN** l'utilisateur scroll dans une liste de 1 million de fichiers
- **THEN** les nouveaux éléments sont chargés à la demande depuis la base de données
- **THEN** la mémoire utilisée reste constante

### Requirement: Colonnes de la liste
Le système SHALL afficher les colonnes suivantes pour chaque fichier : Nom, Répertoire, Extension, Taille, Date de modification.

#### Scenario: Affichage des colonnes
- **WHEN** la liste est affichée
- **THEN** chaque ligne contient le nom du fichier, son répertoire parent, son extension, sa taille formatée et sa date de modification

### Requirement: Tri par colonnes
Le système SHALL permettre de trier la liste en cliquant sur les headers de colonnes.

#### Scenario: Tri par nom
- **WHEN** l'utilisateur clique sur le header "Nom"
- **THEN** la liste est triée par nom en ordre ascendant
- **THEN** un indicateur visuel (▲) apparaît sur le header

#### Scenario: Inverser le tri
- **WHEN** l'utilisateur clique à nouveau sur le header "Nom" déjà trié ascendant
- **THEN** la liste est triée par nom en ordre descendant
- **THEN** l'indicateur visuel change (▼)

#### Scenario: Changer de colonne de tri
- **WHEN** l'utilisateur clique sur le header "Taille" alors que le tri est sur "Nom"
- **THEN** la liste est triée par taille en ordre descendant (les gros fichiers d'abord)
- **THEN** l'indicateur de tri se déplace sur "Taille"

### Requirement: Tri par défaut selon le contexte
Le système SHALL utiliser un tri par défaut approprié selon le contexte de recherche.

#### Scenario: Sans recherche
- **WHEN** aucune recherche n'est active
- **THEN** le tri par défaut est par nom ascendant (A-Z)

#### Scenario: Avec recherche FTS
- **WHEN** une recherche textuelle est active
- **THEN** le tri par défaut est par pertinence (rank FTS)
- **THEN** l'utilisateur peut changer le tri manuellement

### Requirement: Recherche filtre la liste
Le système SHALL filtrer la liste virtualisée en temps réel selon la recherche.

#### Scenario: Recherche textuelle
- **WHEN** l'utilisateur tape "rapport" dans la barre de recherche
- **THEN** la liste n'affiche que les fichiers correspondant à la recherche
- **THEN** le compteur dans le footer reflète le nombre de résultats

#### Scenario: Effacer la recherche
- **WHEN** l'utilisateur efface le texte de recherche
- **THEN** la liste affiche à nouveau tous les fichiers
