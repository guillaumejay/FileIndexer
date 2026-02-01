## 1. Infrastructure et dépendances

- [x] 1.1 ~~Ajouter le package NuGet Microsoft.WindowsAPICodePack-Shell~~ (retiré - incompatible cross-platform, utilise FolderBrowser custom)
- [x] 1.2 Créer le service FileOperationsService avec injection de dépendances (Singleton)
- [x] 1.3 Ajouter les méthodes DB dans IndexDbContext : UpdateFilePathAsync, InsertSingleFileAsync, GetFilesByIdsAsync, DeleteFilesByIdsAsync

## 2. Sélecteur de dossier cross-platform

- [x] 2.1 Implémenter IFolderPickerService avec méthode PickFolderAsync
- [x] 2.2 ~~Implémenter WindowsFolderPicker utilisant CommonOpenFileDialog~~ (utilise FallbackFolderPicker sur toutes les plateformes)
- [x] 2.3 Implémenter FallbackFolderPicker utilisant le composant FolderBrowser existant
- [x] 2.4 Enregistrer le service avec détection d'OS au démarrage

## 3. Service de corbeille cross-platform

- [x] 3.1 Créer l'interface ITrashService avec méthode MoveToTrashAsync(string path)
- [x] 3.2 Implémenter WindowsTrashService (Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile avec SendToRecycleBin)
- [x] 3.3 Implémenter LinuxTrashService (appel à trash-put avec détection d'installation)
- [x] 3.4 Implémenter MacTrashService (appel osascript pour Finder trash)
- [x] 3.5 Enregistrer le service approprié selon l'OS au démarrage

## 4. Multi-sélection dans SearchView

- [x] 4.1 Ajouter l'état de sélection (HashSet<long> selectedIds, long? anchorId)
- [x] 4.2 Implémenter le clic simple (sélection unique, désélection des autres)
- [x] 4.3 Implémenter Ctrl+clic (toggle sélection)
- [x] 4.4 Implémenter Shift+clic (sélection de plage)
- [x] 4.5 Ajouter le style CSS pour la surbrillance des lignes sélectionnées
- [x] 4.6 Afficher le compteur de fichiers sélectionnés dans l'interface

## 5. Actions double-clic

- [x] 5.1 Implémenter FileOperationsService.OpenFileAsync (Process.Start)
- [x] 5.2 Implémenter FileOperationsService.OpenFolderAsync (explorer.exe /select, xdg-open, open)
- [x] 5.3 Ajouter @ondblclick sur la cellule Nom dans SearchView
- [x] 5.4 Ajouter @ondblclick sur la cellule Répertoire dans SearchView
- [x] 5.5 Gérer les erreurs (fichier/dossier inexistant) avec dialogue

## 6. Menu contextuel

- [x] 6.1 Créer le menu contextuel inline dans SearchView (position absolue, liste d'options)
- [x] 6.2 Ajouter @oncontextmenu sur les lignes de SearchView
- [x] 6.3 Gérer la logique de sélection au clic droit (conserver ou remplacer sélection)
- [x] 6.4 Implémenter la fermeture du menu (clic extérieur, Échap, sélection option)
- [x] 6.5 Afficher les options selon le contexte : Renommer (1 fichier), Copier, Déplacer, Supprimer

## 7. Renommage inline

- [x] 7.1 Ajouter l'état d'édition (long? editingFileId, string editingName)
- [x] 7.2 Remplacer la cellule Nom par un input quand en mode édition
- [x] 7.3 Pré-sélectionner le nom sans extension à l'entrée en édition
- [x] 7.4 Déclencher l'édition via menu contextuel "Renommer"
- [x] 7.5 Déclencher l'édition via touche F2 quand un fichier est sélectionné
- [x] 7.6 Déclencher l'édition via double-clic lent (500-1500ms entre les clics)
- [x] 7.7 Gérer Entrée (confirmer), Échap (annuler), blur (confirmer)
- [x] 7.8 Valider le nom (caractères interdits, non vide)
- [x] 7.9 Implémenter FileOperationsService.RenameFileAsync avec synchro DB

## 8. Dialogue de conflit de nom

- [x] 8.1 Créer le composant ConflictDialog.razor (Remplacer / Garder les deux / Annuler)
- [x] 8.2 Implémenter la logique de génération de nom unique (fichier (1).ext)
- [x] 8.3 Intégrer le dialogue dans les opérations Copier/Déplacer/Renommer

## 9. Opérations Copier et Déplacer

- [x] 9.1 Implémenter FileOperationsService.CopyFilesAsync avec synchro DB
- [x] 9.2 Implémenter FileOperationsService.MoveFilesAsync avec synchro DB
- [x] 9.3 Connecter l'option "Copier vers..." du menu au sélecteur de dossier puis à CopyFiles
- [x] 9.4 Connecter l'option "Déplacer vers..." du menu au sélecteur de dossier puis à MoveFiles
- [x] 9.5 Rafraîchir la liste après opération réussie

## 10. Opération Supprimer

- [x] 10.1 Implémenter FileOperationsService.DeleteFilesAsync utilisant ITrashService + synchro DB
- [x] 10.2 Connecter l'option "Supprimer" du menu à DeleteFiles
- [x] 10.3 Connecter la touche Suppr (Delete) à DeleteFiles quand fichiers sélectionnés
- [x] 10.4 Rafraîchir la liste après suppression réussie

## 11. Dialogue d'erreur

- [x] 11.1 Créer le composant ErrorDialog.razor (message + bouton OK)
- [x] 11.2 Intégrer le dialogue pour toutes les opérations qui peuvent échouer
- [x] 11.3 Message spécifique pour trash-cli non installé avec instructions

## 12. Tests et polish

- [ ] 12.1 Tester les opérations sur Windows (dialogue natif, corbeille)
- [ ] 12.2 Tester le fallback sur Linux (trash-cli) si applicable
- [ ] 12.3 Tester le fallback sur macOS (osascript) si applicable
- [ ] 12.4 Vérifier la synchronisation DB après chaque type d'opération
- [ ] 12.5 Vérifier le comportement avec fichiers verrouillés ou permissions insuffisantes
- [ ] 12.6 Vérifier le renommage via F2 et double-clic lent
