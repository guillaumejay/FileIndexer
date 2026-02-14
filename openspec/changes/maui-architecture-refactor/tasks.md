## 1. Restructuration de la solution

- [x] 1.1 Créer le dossier src/ et déplacer le projet actuel dedans
- [x] 1.2 Renommer le projet actuel en FileIndexer.Web
- [x] 1.3 Mettre à jour FileIndexer.sln avec les nouveaux chemins

## 2. Création de FileIndexer.Core

- [x] 2.1 Créer le projet FileIndexer.Core (class library, net10.0)
- [x] 2.2 Ajouter les packages NuGet (Microsoft.Data.Sqlite, Dapper)
- [x] 2.3 Déplacer Models/IndexedFile.cs vers Core
- [x] 2.4 Déplacer Models/Collection.cs vers Core
- [x] 2.5 Déplacer Data/IndexDbContext.cs vers Core
- [x] 2.6 Déplacer Services/SearchService.cs vers Core
- [x] 2.7 Extraire les méthodes read-only de CollectionService vers Core (déplacé entièrement)
- [x] 2.8 Ajouter la référence Core dans FileIndexer.Web
- [x] 2.9 Mettre à jour les namespaces et usings dans Web

## 3. Création de FileIndexer.Desktop

- [x] 3.1 Créer le projet FileIndexer.Desktop (class library, net10.0)
- [x] 3.2 Ajouter la référence vers FileIndexer.Core
- [x] 3.3 Déplacer Services/FileScannerService.cs vers Desktop
- [x] 3.4 Déplacer Services/FileOperationsService.cs vers Desktop
- [x] 3.5 Déplacer Services/ITrashService.cs vers Desktop
- [x] 3.6 Déplacer Services/WindowsTrashService.cs vers Desktop
- [x] 3.7 Déplacer Services/LinuxTrashService.cs vers Desktop
- [x] 3.8 Déplacer Services/MacTrashService.cs vers Desktop
- [x] 3.9 Déplacer Services/IFolderPickerService.cs vers Desktop (ou Web) - reste dans Web
- [x] 3.10 Déplacer Services/FallbackFolderPicker.cs vers Web (UI-specific) - déjà dans Web
- [x] 3.11 Ajouter la référence Desktop dans FileIndexer.Web
- [x] 3.12 Mettre à jour les namespaces et usings dans Web

## 4. Validation de la refactorisation

- [x] 4.1 Vérifier que dotnet build compile sans erreur
- [x] 4.2 Vérifier que FileIndexer.Web démarre correctement
- [x] 4.3 Tester la recherche de fichiers
- [x] 4.4 Tester l'indexation d'un dossier
- [x] 4.5 Tester les opérations fichiers (rename, copy, move, delete)

## 5. Création de FileIndexer.Maui

- [x] 5.1 Créer le projet FileIndexer.Maui (MAUI Blazor Hybrid template)
- [x] 5.2 Configurer les target frameworks net10.0 (android, ios, maccatalyst, windows)
- [x] 5.3 Ajouter la référence vers FileIndexer.Core
- [x] 5.4 Créer le service de configuration DatabasePathService
- [x] 5.5 Implémenter l'écran de sélection du fichier .db (file picker)
- [x] 5.6 Persister le chemin dans Preferences

## 6. UI Mobile

- [x] 6.1 Copier les styles CSS de base depuis Web
- [x] 6.2 Créer MainLayout.razor adapté pour mobile
- [x] 6.3 Créer SearchView.razor simplifié (sans actions de modification)
- [x] 6.4 Implémenter le long-press pour copier le chemin
- [x] 6.5 Ajouter le toast de confirmation de copie
- [x] 6.6 Adapter les tailles de touch targets (min 44px)
- [x] 6.7 Créer l'écran Settings pour changer le fichier .db

## 7. Full features sur Windows/macOS

- [x] 7.1 Ajouter référence conditionnelle Desktop dans MAUI (Windows/macOS)
- [x] 7.2 Enregistrer les services Desktop dans MauiProgram.cs (conditionnel)
- [x] 7.3 Créer IFolderPickerService MAUI avec dialogues natifs
- [x] 7.4 Adapter Home.razor avec features complètes sur desktop
- [x] 7.5 Ajouter le scan/indexation sur desktop
- [x] 7.6 Ajouter les actions fichiers (rename, copy, move, delete) sur desktop

## 8. Tests et finalisation

> Note: Exécuter `dotnet workload install maui` avant de tester

- [ ] 8.1 Tester sur Windows (features complètes)
- [ ] 8.2 Tester sur émulateur Android (read-only)
- [ ] 8.3 Vérifier les dialogues natifs Windows
- [ ] 8.4 Build de release Windows
