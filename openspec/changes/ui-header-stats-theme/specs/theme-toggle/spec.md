## ADDED Requirements

### Requirement: Bouton toggle thème
Le système SHALL afficher un bouton permettant de basculer entre le thème clair et sombre.

#### Scenario: Affichage du bouton
- **WHEN** la page est affichée
- **THEN** un bouton avec une icône soleil/lune est visible dans le header

### Requirement: Basculement de thème
Le système SHALL changer l'apparence de l'application lors du clic sur le bouton.

#### Scenario: Passer en mode jour
- **WHEN** l'utilisateur clique sur le bouton en mode nuit
- **THEN** l'application passe en thème clair (fond clair, texte sombre)
- **THEN** l'icône change pour indiquer le mode actuel

#### Scenario: Passer en mode nuit
- **WHEN** l'utilisateur clique sur le bouton en mode jour
- **THEN** l'application passe en thème sombre (fond sombre, texte clair)
- **THEN** l'icône change pour indiquer le mode actuel

### Requirement: Persistance du thème
Le système SHALL mémoriser le choix de thème de l'utilisateur.

#### Scenario: Rechargement de page
- **WHEN** l'utilisateur a choisi le mode jour et recharge la page
- **THEN** l'application s'affiche en mode jour

#### Scenario: Nouvelle session
- **WHEN** l'utilisateur revient sur l'application après avoir fermé le navigateur
- **THEN** l'application utilise le dernier thème choisi
