## ADDED Requirements

### Requirement: Bouton d'accès aux paramètres
Le système SHALL afficher un bouton d'accès aux paramètres dans la barre de recherche.

#### Scenario: Affichage du bouton
- **WHEN** la page d'accueil est affichée
- **THEN** un bouton settings (icône engrenage) est visible à droite de la barre de recherche

### Requirement: Ouverture de la modal
Le système SHALL ouvrir une modal centrée lorsque l'utilisateur clique sur le bouton settings.

#### Scenario: Clic sur le bouton settings
- **WHEN** l'utilisateur clique sur le bouton settings
- **THEN** une modal centrée s'affiche par-dessus la liste
- **THEN** un overlay semi-transparent couvre le reste de la page

### Requirement: Fermeture de la modal
Le système SHALL permettre de fermer la modal de plusieurs façons.

#### Scenario: Fermeture par bouton X
- **WHEN** l'utilisateur clique sur le bouton X de la modal
- **THEN** la modal se ferme

#### Scenario: Fermeture par clic sur l'overlay
- **WHEN** l'utilisateur clique sur l'overlay semi-transparent
- **THEN** la modal se ferme

#### Scenario: Fermeture par touche Escape
- **WHEN** l'utilisateur appuie sur la touche Escape
- **THEN** la modal se ferme

### Requirement: Contenu de la modal d'indexation
La modal SHALL contenir les contrôles d'indexation : chemin à scanner, option de scan incrémental, et bouton de lancement.

#### Scenario: Affichage des contrôles
- **WHEN** la modal est ouverte
- **THEN** un champ de saisie pour le chemin à indexer est affiché
- **THEN** une case à cocher pour le scan incrémental est affichée
- **THEN** un bouton "Démarrer le scan" est affiché

#### Scenario: Chemin pré-rempli
- **WHEN** la modal est ouverte
- **THEN** le champ chemin contient la valeur par défaut de la configuration

### Requirement: Lancement du scan depuis la modal
Le système SHALL permettre de lancer un scan depuis la modal.

#### Scenario: Démarrer un scan
- **WHEN** l'utilisateur clique sur "Démarrer le scan" avec un chemin valide
- **THEN** le scan démarre
- **THEN** le bouton devient "Annuler"

#### Scenario: Scan avec chemin vide
- **WHEN** le champ chemin est vide
- **THEN** le bouton "Démarrer le scan" est désactivé

### Requirement: Progression du scan dans la modal
Le système SHALL afficher la progression du scan dans la modal.

#### Scenario: Affichage de la progression
- **WHEN** un scan est en cours
- **THEN** une barre de progression est affichée
- **THEN** le nombre de fichiers et répertoires scannés est affiché
- **THEN** le répertoire en cours de scan est affiché

#### Scenario: Scan terminé
- **WHEN** le scan se termine
- **THEN** un message de succès est affiché
- **THEN** la liste de fichiers est rafraîchie

### Requirement: Annulation du scan
Le système SHALL permettre d'annuler un scan en cours.

#### Scenario: Annuler le scan
- **WHEN** l'utilisateur clique sur "Annuler" pendant un scan
- **THEN** le scan s'arrête
- **THEN** le bouton redevient "Démarrer le scan"
