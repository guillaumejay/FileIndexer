## ADDED Requirements

### Requirement: Affichage du menu contextuel
Le système SHALL afficher un menu contextuel quand l'utilisateur fait un clic droit sur une ligne de la liste.

#### Scenario: Clic droit sur un fichier non sélectionné
- **WHEN** l'utilisateur fait un clic droit sur un fichier qui n'est pas dans la sélection
- **THEN** ce fichier devient le seul fichier sélectionné
- **THEN** le menu contextuel s'affiche à la position du curseur

#### Scenario: Clic droit sur un fichier de la sélection multiple
- **WHEN** l'utilisateur a plusieurs fichiers sélectionnés
- **WHEN** l'utilisateur fait un clic droit sur un fichier de la sélection
- **THEN** la sélection multiple est conservée
- **THEN** le menu contextuel s'affiche

### Requirement: Options du menu contextuel
Le système SHALL proposer les options appropriées selon le contexte de sélection.

#### Scenario: Menu pour un fichier unique
- **WHEN** un seul fichier est sélectionné
- **THEN** le menu affiche : "Renommer", "Copier vers...", "Déplacer vers...", "Supprimer"

#### Scenario: Menu pour sélection multiple
- **WHEN** plusieurs fichiers sont sélectionnés
- **THEN** le menu affiche : "Copier vers...", "Déplacer vers...", "Supprimer"
- **THEN** l'option "Renommer" n'est pas disponible

### Requirement: Fermeture du menu contextuel
Le système SHALL fermer le menu contextuel dans les situations appropriées.

#### Scenario: Clic en dehors du menu
- **WHEN** le menu contextuel est ouvert
- **WHEN** l'utilisateur clique en dehors du menu
- **THEN** le menu se ferme

#### Scenario: Sélection d'une option
- **WHEN** l'utilisateur clique sur une option du menu
- **THEN** le menu se ferme
- **THEN** l'action correspondante est déclenchée

#### Scenario: Touche Échap
- **WHEN** le menu contextuel est ouvert
- **WHEN** l'utilisateur appuie sur Échap
- **THEN** le menu se ferme
