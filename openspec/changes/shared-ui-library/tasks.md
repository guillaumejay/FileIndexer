## 1. Bibliothèque partagée

- [x] 1.1 Créer FileIndexer.UI (Razor Class Library) et l'ajouter à la solution
- [x] 1.2 `PlatformCapabilities`, `AddFileIndexer`, `FileIndexerJs`, `INativeFolderPicker`, `IConfigFileExchange`
- [x] 1.3 Composants : AppShell, SearchView, CollectionsView, CollectionEditor, ActivityIndicator, FolderBrowser, Dialogs, Modal, Toast
- [x] 1.4 CSS fusionné (`fileindexer.css`, règles mortes retirées) et JS partagé (`fileindexer.js`)

## 2. Hôtes

- [x] 2.1 Web : une page `Home`, page `/Error`, `MapStaticAssets`, réglages de scan appliqués, `AllowedHosts` limité à localhost
- [x] 2.2 MAUI : `DatabaseManager` (bascule à chaud, copie locale sur mobile), dialogues natifs, `DatabaseSettings`
- [x] 2.3 Supprimer les pages et services dupliqués des deux hôtes

## 3. Correctifs UI (faits une fois dans les composants partagés)

- [x] 3.1 Suppr. dans la recherche n'agit plus sur la sélection ; confirmation avant mise à la corbeille
- [x] 3.2 Recherche différée sans `async void` ni timer ; erreurs de recherche affichées au lieu de faire tomber le circuit
- [x] 3.3 Sélection conservée hors de la fenêtre virtualisée ; Shift+clic résolu côté index
- [x] 3.4 Plus d'appel à un composant détruit après changement d'onglet
- [x] 3.5 Toasts sur le dispatcher, non écrasés ; `Dispose` des composants appelé
- [x] 3.6 Collections en lecture seule sans accès au système de fichiers

## 4. Vérification

- [x] 4.1 `dotnet build` (Web, MAUI Windows + Android) sans avertissement
- [x] 4.2 Tests bUnit des composants partagés
- [ ] 4.3 Vérification manuelle MAUI (Windows et Android)
