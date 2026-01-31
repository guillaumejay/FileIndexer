## ADDED Requirements

### Requirement: Barre de statut en footer
Le système SHALL afficher une barre de statut fixe en bas de l'écran.

#### Scenario: Position du footer
- **WHEN** la page est affichée
- **THEN** une barre de statut est visible en bas de l'écran
- **THEN** la barre reste fixe lors du scroll de la liste

### Requirement: Nombre de fichiers affichés
Le système SHALL afficher le nombre de fichiers correspondant à la recherche actuelle.

#### Scenario: Sans recherche
- **WHEN** aucune recherche n'est active
- **THEN** le footer affiche le nombre total de fichiers indexés (ex: "1 234 567 fichiers")

#### Scenario: Avec recherche
- **WHEN** une recherche est active avec 150 résultats
- **THEN** le footer affiche "150 fichiers" (le nombre de résultats)

### Requirement: Statistiques de l'index
Le système SHALL afficher les statistiques globales de l'index dans le footer.

#### Scenario: Affichage des stats
- **WHEN** la page est affichée
- **THEN** le footer affiche le nombre total de fichiers indexés
- **THEN** le footer affiche la taille de la base de données (ex: "45 MB")

### Requirement: Date de dernière indexation
Le système SHALL afficher la date de dernière indexation dans le footer.

#### Scenario: Index existant
- **WHEN** l'index contient des fichiers
- **THEN** le footer affiche la date de dernière indexation (ex: "Indexé il y a 2h")

#### Scenario: Index vide
- **WHEN** l'index est vide
- **THEN** le footer affiche "Aucun fichier indexé"
