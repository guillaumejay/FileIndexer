## ADDED Requirements

### Requirement: Sélection simple au clic
Le système SHALL sélectionner un fichier unique quand l'utilisateur clique dessus sans modificateur.

#### Scenario: Clic simple sur un fichier
- **WHEN** l'utilisateur clique sur une ligne de fichier
- **THEN** ce fichier devient le seul fichier sélectionné
- **THEN** la ligne est visuellement mise en surbrillance

#### Scenario: Clic simple désélectionne les autres
- **WHEN** plusieurs fichiers sont sélectionnés
- **WHEN** l'utilisateur clique sur un fichier sans modificateur
- **THEN** tous les autres fichiers sont désélectionnés
- **THEN** seul le fichier cliqué est sélectionné

### Requirement: Multi-sélection avec Ctrl+clic
Le système SHALL permettre d'ajouter ou retirer un fichier de la sélection avec Ctrl+clic.

#### Scenario: Ctrl+clic ajoute à la sélection
- **WHEN** l'utilisateur maintient Ctrl et clique sur un fichier non sélectionné
- **THEN** ce fichier est ajouté à la sélection existante
- **THEN** les fichiers précédemment sélectionnés restent sélectionnés

#### Scenario: Ctrl+clic retire de la sélection
- **WHEN** l'utilisateur maintient Ctrl et clique sur un fichier déjà sélectionné
- **THEN** ce fichier est retiré de la sélection
- **THEN** les autres fichiers sélectionnés restent sélectionnés

### Requirement: Sélection de plage avec Shift+clic
Le système SHALL permettre de sélectionner une plage de fichiers avec Shift+clic.

#### Scenario: Shift+clic sélectionne une plage
- **WHEN** un fichier est sélectionné (anchor)
- **WHEN** l'utilisateur maintient Shift et clique sur un autre fichier
- **THEN** tous les fichiers entre l'anchor et le fichier cliqué sont sélectionnés

#### Scenario: Shift+clic remplace la sélection précédente
- **WHEN** plusieurs fichiers non contigus sont sélectionnés
- **WHEN** l'utilisateur fait Shift+clic
- **THEN** la sélection précédente est remplacée par la nouvelle plage

### Requirement: Indication visuelle de la sélection
Le système SHALL indiquer visuellement quels fichiers sont sélectionnés.

#### Scenario: Fichier sélectionné
- **WHEN** un fichier est dans la sélection
- **THEN** sa ligne a une couleur de fond distincte (surbrillance)

#### Scenario: Compteur de sélection
- **WHEN** plusieurs fichiers sont sélectionnés
- **THEN** le nombre de fichiers sélectionnés est affiché dans l'interface
