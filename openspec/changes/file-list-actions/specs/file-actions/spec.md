## ADDED Requirements

### Requirement: Ouvrir un fichier au double-clic
Le système SHALL ouvrir un fichier avec son application par défaut quand l'utilisateur double-clique sur le nom du fichier dans la liste.

#### Scenario: Double-clic sur le nom d'un fichier
- **WHEN** l'utilisateur double-clique sur la cellule "Nom" d'un fichier
- **THEN** le fichier est ouvert avec l'application par défaut du système
- **THEN** l'interface reste réactive

#### Scenario: Fichier inexistant
- **WHEN** l'utilisateur double-clique sur un fichier qui n'existe plus sur le disque
- **THEN** un dialogue d'erreur s'affiche avec le message "Le fichier n'existe plus"

### Requirement: Ouvrir le dossier parent au double-clic
Le système SHALL ouvrir l'explorateur de fichiers au dossier parent quand l'utilisateur double-clique sur la colonne répertoire.

#### Scenario: Double-clic sur le répertoire
- **WHEN** l'utilisateur double-clique sur la cellule "Répertoire" d'un fichier
- **THEN** l'explorateur de fichiers s'ouvre au dossier parent
- **THEN** le fichier concerné est sélectionné dans l'explorateur (si supporté par l'OS)

#### Scenario: Dossier inexistant
- **WHEN** l'utilisateur double-clique sur un répertoire qui n'existe plus
- **THEN** un dialogue d'erreur s'affiche avec le message "Le dossier n'existe plus"
