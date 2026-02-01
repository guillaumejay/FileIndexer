## ADDED Requirements

### Requirement: Renommer un fichier
Le système SHALL permettre de renommer un fichier via le menu contextuel, la touche F2, ou un double-clic lent, avec édition inline.

#### Scenario: Renommage via menu contextuel
- **WHEN** l'utilisateur sélectionne "Renommer" dans le menu contextuel
- **THEN** la cellule du nom devient un champ de saisie éditable
- **THEN** le nom actuel est pré-sélectionné (sans l'extension)

#### Scenario: Renommage via F2
- **WHEN** un fichier est sélectionné
- **WHEN** l'utilisateur appuie sur F2
- **THEN** la cellule du nom devient un champ de saisie éditable

#### Scenario: Renommage via double-clic lent
- **WHEN** l'utilisateur clique sur le nom d'un fichier déjà sélectionné
- **WHEN** l'utilisateur clique à nouveau après 500ms (mais avant 1500ms)
- **THEN** la cellule du nom devient un champ de saisie éditable
- **THEN** ce comportement est distinct du double-clic rapide qui ouvre le fichier

#### Scenario: Confirmation du renommage
- **WHEN** l'utilisateur modifie le nom et appuie sur Entrée
- **THEN** le fichier est renommé sur le disque
- **THEN** la base de données est mise à jour avec le nouveau chemin

#### Scenario: Annulation du renommage
- **WHEN** l'utilisateur appuie sur Échap pendant l'édition
- **THEN** le renommage est annulé
- **THEN** le nom original est restauré

#### Scenario: Nom invalide
- **WHEN** l'utilisateur entre un nom contenant des caractères interdits (\ / : * ? " < > |)
- **THEN** un message d'erreur s'affiche
- **THEN** le champ reste en mode édition

#### Scenario: Conflit de nom au renommage
- **WHEN** l'utilisateur entre un nom qui existe déjà dans le même dossier
- **THEN** un dialogue de conflit s'affiche avec les options "Remplacer", "Garder les deux", "Annuler"

### Requirement: Copier des fichiers
Le système SHALL permettre de copier un ou plusieurs fichiers vers un dossier de destination.

#### Scenario: Copie d'un fichier unique
- **WHEN** l'utilisateur sélectionne "Copier" dans le menu contextuel d'un fichier
- **THEN** un sélecteur de dossier s'affiche
- **WHEN** l'utilisateur sélectionne un dossier de destination
- **THEN** le fichier est copié vers la destination
- **THEN** une nouvelle entrée est ajoutée à la base de données pour la copie

#### Scenario: Copie de fichiers multiples
- **WHEN** l'utilisateur a sélectionné plusieurs fichiers et choisit "Copier"
- **THEN** tous les fichiers sélectionnés sont copiés vers la destination
- **THEN** une entrée DB est créée pour chaque fichier copié

#### Scenario: Conflit de nom à la copie
- **WHEN** un fichier avec le même nom existe à la destination
- **THEN** un dialogue de conflit s'affiche
- **WHEN** l'utilisateur choisit "Garder les deux"
- **THEN** le fichier est copié avec un suffixe numérique (ex: "fichier (1).pdf")

#### Scenario: Échec de copie
- **WHEN** la copie échoue (permissions, espace disque, etc.)
- **THEN** un dialogue d'erreur s'affiche avec le message système
- **THEN** la base de données n'est pas modifiée

### Requirement: Déplacer des fichiers
Le système SHALL permettre de déplacer un ou plusieurs fichiers vers un dossier de destination.

#### Scenario: Déplacement d'un fichier unique
- **WHEN** l'utilisateur sélectionne "Déplacer" dans le menu contextuel d'un fichier
- **THEN** un sélecteur de dossier s'affiche
- **WHEN** l'utilisateur sélectionne un dossier de destination
- **THEN** le fichier est déplacé vers la destination
- **THEN** l'entrée existante dans la base de données est mise à jour avec le nouveau chemin

#### Scenario: Déplacement de fichiers multiples
- **WHEN** l'utilisateur a sélectionné plusieurs fichiers et choisit "Déplacer"
- **THEN** tous les fichiers sélectionnés sont déplacés vers la destination
- **THEN** chaque entrée DB est mise à jour avec le nouveau chemin

#### Scenario: Conflit de nom au déplacement
- **WHEN** un fichier avec le même nom existe à la destination
- **THEN** un dialogue de conflit s'affiche avec les options "Remplacer", "Garder les deux", "Annuler"

#### Scenario: Échec de déplacement
- **WHEN** le déplacement échoue (permissions, fichier verrouillé, etc.)
- **THEN** un dialogue d'erreur s'affiche avec le message système
- **THEN** la base de données n'est pas modifiée

### Requirement: Supprimer des fichiers
Le système SHALL permettre de supprimer un ou plusieurs fichiers vers la corbeille OS.

#### Scenario: Suppression d'un fichier unique
- **WHEN** l'utilisateur sélectionne "Supprimer" dans le menu contextuel d'un fichier
- **THEN** le fichier est envoyé à la corbeille OS
- **THEN** l'entrée est supprimée de la base de données

#### Scenario: Suppression de fichiers multiples
- **WHEN** l'utilisateur a sélectionné plusieurs fichiers et choisit "Supprimer"
- **THEN** tous les fichiers sélectionnés sont envoyés à la corbeille
- **THEN** chaque entrée est supprimée de la base de données

#### Scenario: Suppression via touche Suppr
- **WHEN** un ou plusieurs fichiers sont sélectionnés
- **WHEN** l'utilisateur appuie sur la touche Suppr (Delete)
- **THEN** les fichiers sélectionnés sont envoyés à la corbeille

#### Scenario: Échec de suppression
- **WHEN** la suppression échoue (fichier verrouillé, corbeille indisponible, etc.)
- **THEN** un dialogue d'erreur s'affiche avec le message explicite
- **THEN** la base de données n'est pas modifiée
