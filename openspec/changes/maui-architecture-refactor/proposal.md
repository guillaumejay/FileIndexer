## Why

L'application actuelle (Blazor Server) fonctionne uniquement sur desktop via navigateur. Les utilisateurs veulent pouvoir consulter leur index de fichiers depuis leur téléphone (Android/iOS) pour retrouver rapidement où se trouve un fichier sur leur NAS, même en déplacement. La base de données sera synchronisée via OneDrive/Dropbox.

## What Changes

- **Réorganisation en multi-projets** : Extraction du code partageable dans des bibliothèques séparées
- **Nouveau projet FileIndexer.Core** : Contient Models, Data, et services de lecture (Search, Collections)
- **Nouveau projet FileIndexer.Desktop** : Contient les services desktop-only (Scanner, FileOperations, Trash)
- **Adaptation FileIndexer.Web** : Référence Core + Desktop, garde les composants UI web
- **Nouveau projet FileIndexer.Maui** : Application Blazor Hybrid pour Windows/macOS/Android/iOS (lecture seule)
- Composants UI copiés et adaptés pour mobile (pas de bibliothèque partagée)

## Capabilities

### New Capabilities
- `multi-project-structure`: Architecture multi-projets avec séparation Core/Desktop/Web/Maui
- `maui-mobile-app`: Application MAUI Blazor Hybrid pour consultation de l'index (lecture seule)

### Modified Capabilities
<!-- Aucune modification des specs existantes - c'est une refactorisation architecturale -->

## Impact

- **Structure solution** : Passage d'un projet unique à 4 projets (Core, Desktop, Web, Maui)
- **Dépendances** :
  - Core : Microsoft.Data.Sqlite, Dapper
  - Desktop : Référence Core
  - Web : Référence Core + Desktop
  - Maui : Référence Core uniquement + packages MAUI
- **Fichiers déplacés** : Models/, Data/, Services/ répartis entre Core et Desktop
- **Compatibilité** :
  - Web reste compatible Windows/Linux/macOS
  - Maui cible Windows/macOS/Android/iOS (pas Linux)
