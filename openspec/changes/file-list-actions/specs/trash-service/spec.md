## ADDED Requirements

### Requirement: Suppression vers corbeille Windows
Le système SHALL utiliser l'API Windows native pour envoyer les fichiers à la corbeille sur Windows.

#### Scenario: Suppression réussie sur Windows
- **WHEN** l'utilisateur supprime un fichier sur Windows
- **THEN** le fichier est envoyé à la Corbeille Windows
- **THEN** le fichier peut être restauré depuis la Corbeille

#### Scenario: Fichier verrouillé
- **WHEN** le fichier est verrouillé par une autre application
- **THEN** un dialogue d'erreur s'affiche avec le message système
- **THEN** le fichier reste en place

### Requirement: Suppression vers corbeille Linux
Le système SHALL utiliser trash-cli pour envoyer les fichiers à la corbeille sur Linux.

#### Scenario: Suppression réussie sur Linux
- **WHEN** l'utilisateur supprime un fichier sur Linux
- **WHEN** trash-cli est installé
- **THEN** le fichier est envoyé à la corbeille freedesktop

#### Scenario: trash-cli non installé
- **WHEN** l'utilisateur tente de supprimer un fichier sur Linux
- **WHEN** trash-cli n'est pas installé
- **THEN** un dialogue d'erreur s'affiche avec le message "trash-cli n'est pas installé"
- **THEN** le message inclut les instructions d'installation
- **THEN** aucun fichier n'est supprimé

### Requirement: Suppression vers corbeille macOS
Le système SHALL utiliser osascript pour envoyer les fichiers à la corbeille sur macOS.

#### Scenario: Suppression réussie sur macOS
- **WHEN** l'utilisateur supprime un fichier sur macOS
- **THEN** le fichier est envoyé à la Corbeille via Finder
- **THEN** le fichier peut être restauré depuis la Corbeille

#### Scenario: Erreur AppleScript
- **WHEN** l'exécution d'osascript échoue
- **THEN** un dialogue d'erreur s'affiche avec le message d'erreur
- **THEN** aucun fichier n'est supprimé
