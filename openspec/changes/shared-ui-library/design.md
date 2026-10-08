## Context

Deux hôtes Blazor (Server et Hybrid) affichent la même application. Seuls diffèrent : l'accès aux fichiers indexés (oui sur desktop, non sur mobile), les dialogues natifs et le choix de la base (MAUI).

## Decisions

### D1 : une Razor Class Library plutôt que des copies
Les composants vivent dans `FileIndexer.UI`. Un hôte ne contient ni page dupliquée ni CSS applicatif.

### D2 : capacités au runtime plutôt que `#if`
`PlatformCapabilities.HasFileSystemAccess` décide si les services Desktop sont enregistrés (`AddFileIndexer`) ; les composants les résolvent via `IServiceProvider` et masquent les actions absentes. Conséquence assumée : l'assembly Desktop est embarqué sur Android/iOS sans être utilisé.

**Alternative écartée** : interfaces Desktop dans Core pour éviter cette référence — beaucoup de code d'abstraction pour quelques centaines de Ko.

### D3 : services hôtes optionnels
- `INativeFolderPicker` : MAUI desktop ; sinon `FolderBrowser` en page (Web).
- `IConfigFileExchange` : `FileSaver`/`FilePicker` (MAUI) ou téléchargement/envoi navigateur (`JsConfigFileExchange`, Web).
- Presse-papiers et thème passent par JS dans les deux hôtes.

### D4 : un seul hôte de dialogues par vue
`Dialogs` sérialise confirmation, conflit, erreur et choix de dossier (une modale à la fois), ce qui supprime les paires `show*/TaskCompletionSource` répétées.

### D5 : état du scanner partagé
Le scanner est un singleton commun à toutes les sessions Web : l'UI lit `IsRunning` et `ScanProgress.CollectionId` au lieu d'un drapeau local, et le verrou « scan en cours » est atomique.

## Risks

- **Pas de test d'interaction navigateur réel** dans la CI : couvert par bUnit (`UiComponentTests`) et le prérendu ; les hôtes doivent être vérifiés à la main (MAUI Windows/Android).
