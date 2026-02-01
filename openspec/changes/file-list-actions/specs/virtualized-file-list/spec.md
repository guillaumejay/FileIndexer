## MODIFIED Requirements

### Requirement: Liste virtualisée affiche tous les fichiers
Le système SHALL afficher tous les fichiers indexés dans une liste virtualisée qui occupe tout l'espace vertical disponible entre la barre de recherche et le footer. Chaque ligne SHALL répondre aux événements de clic, double-clic et clic droit.

#### Scenario: Chargement initial
- **WHEN** l'utilisateur accède à la page d'accueil
- **THEN** la liste affiche tous les fichiers triés par nom (A-Z)
- **THEN** seuls les éléments visibles sont rendus dans le DOM

#### Scenario: Scroll dans une grande liste
- **WHEN** l'utilisateur scroll dans une liste de 1 million de fichiers
- **THEN** les nouveaux éléments sont chargés à la demande depuis la base de données
- **THEN** la mémoire utilisée reste constante

#### Scenario: Double-clic sur la colonne nom
- **WHEN** l'utilisateur double-clique sur la cellule "Nom" d'un fichier
- **THEN** le fichier est ouvert avec l'application par défaut

#### Scenario: Double-clic sur la colonne répertoire
- **WHEN** l'utilisateur double-clique sur la cellule "Répertoire" d'un fichier
- **THEN** l'explorateur de fichiers s'ouvre au dossier parent

#### Scenario: Clic droit sur une ligne
- **WHEN** l'utilisateur fait un clic droit sur une ligne
- **THEN** le menu contextuel s'affiche à la position du curseur

#### Scenario: Sélection de fichiers
- **WHEN** l'utilisateur clique sur une ligne
- **THEN** le fichier est sélectionné et visuellement mis en surbrillance
- **WHEN** l'utilisateur Ctrl+clique sur d'autres lignes
- **THEN** ces fichiers sont ajoutés à la sélection
