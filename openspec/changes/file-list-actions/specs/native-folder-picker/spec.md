## ADDED Requirements

### Requirement: Sélecteur de dossier natif Windows
Le système SHALL utiliser le dialogue natif Windows pour sélectionner un dossier quand l'application tourne sur Windows.

#### Scenario: Ouverture du sélecteur natif sur Windows
- **WHEN** une opération nécessite de choisir un dossier de destination
- **WHEN** l'application tourne sur Windows
- **THEN** le dialogue CommonOpenFileDialog natif s'ouvre
- **THEN** l'utilisateur peut naviguer dans l'arborescence Windows

#### Scenario: Sélection d'un dossier
- **WHEN** l'utilisateur sélectionne un dossier et clique OK
- **THEN** le chemin complet du dossier est retourné
- **THEN** l'opération continue avec ce dossier

#### Scenario: Annulation du dialogue
- **WHEN** l'utilisateur clique Annuler ou ferme le dialogue
- **THEN** null est retourné
- **THEN** l'opération en cours est annulée

### Requirement: Fallback FolderBrowser sur autres OS
Le système SHALL utiliser le composant FolderBrowser custom quand l'application tourne sur Linux ou macOS.

#### Scenario: Ouverture du sélecteur sur Linux/macOS
- **WHEN** une opération nécessite de choisir un dossier de destination
- **WHEN** l'application tourne sur Linux ou macOS
- **THEN** le composant FolderBrowser modal s'affiche dans le navigateur

#### Scenario: Comportement identique
- **WHEN** l'utilisateur utilise le FolderBrowser
- **THEN** il peut naviguer dans les dossiers, sélectionner un dossier
- **THEN** le résultat (chemin ou null) est traité de la même manière que le dialogue natif
