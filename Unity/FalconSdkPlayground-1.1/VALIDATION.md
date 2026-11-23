# Validation — Playground 1.1.0 / SDK 1.2.0-preview.5

19 novembre 2026. Windows x64, Unity 6000.3.24f1, rendu intégré, Player Mono. Les essais de cette livraison utilisent exclusivement la simulation ; aucun Falcon n’a été sélectionné ou piloté.

## Résultats

| Contrôle | Résultat |
| --- | --- |
| Noyau SDK, `dotnet run --project Tests/Falcon.Core.Tests` | 27 scénarios PASS, dont 48 permutations signées et gestes invalides |
| API Unity, `dotnet build Tests/Unity.ApiCheck --no-restore` | PASS, 0 erreur, 0 avertissement |
| Construction et validation de scène | PASS, SDK embarqué preview.5, références présentes, matériel inactif |
| Test de Play `FalconPlaygroundSmoke.Run` | PASS, retour code 0 |
| Construction Player Windows x64 Mono | PASS, retour code 0 |
| Player compilé avec `--playground-self-test` | PASS, JSON success=true, retour code 0 |
| Inspection visuelle du Player | PASS sur fenêtre observée 1154 × 800 incluant la barre de titre |

Le test Play reproduit droite sur Y brut, haut sur Z brut, vers soi sur X brut. Il exécute les trois gestes, vérifie le refus de force avant confirmation, la projection droite/haut de la caméra, la conservation des axes dans un profil Unity et le JSON v2 avec échelle/inversion. Il contrôle aussi quatre boutons, capture de zone, six faces à 6 mm, friction, diagnostic exporté, déconnexion, reprise désarmée et arrêt des sessions à la sortie de Play.

L’inspection visuelle couvre Appareil, Directions et Essayer, la navigation sans activation parasite, les trois essais principaux et les réglages avancés défilants. Un mouvement simulé à droite déplace le curseur à droite ; le mouvement haut le déplace vers le haut. La vue reste séparée des panneaux et l’arrêt reste visible pendant le défilement. Les traces de rendu aux anciennes limites de caméra ont été corrigées par un fond plein écran.

Le Player autonome vérifie démarrage en simulation, matériel inactif, quatre boutons, mapping et références de surface sans activer d’effort.

## Reproduire

Utiliser `D:/Unity/6000.3.24f1/Editor/Unity.com` avec `-batchmode -nographics -projectPath <ce projet>` et :

- `-executeMethod Falcon.Playground.Editor.FalconPlaygroundBuilder.BuildAndValidate -quit`
- `-executeMethod Falcon.Playground.Editor.FalconPlaygroundSmoke.Run` (sans `-quit`, le test quitte lui-même)
- `-executeMethod Falcon.Playground.Editor.FalconPlaygroundBuilder.BuildWindows -quit`

Les tests écrivent dans Temp/PlaygroundSmoke, sans modifier les réglages persistants de l’utilisateur. Le test de scène reconstruit la scène générée : employer une copie si elle a été personnalisée.

## Limites et observations

Les journaux Unity ont aussi émis une exception interne d’indexation `UnityEditor.Search.SearchDatabase` et des messages Mono de fin de processus, malgré les assertions PASS et codes 0. Ils ne sont pas présentés comme des journaux entièrement exempts de bruit interne. Aucun avertissement de compilation C# dans la dernière construction.

Les gestes et forces sur le matériel, la précision physique, les coupures matérielles, IL2CPP et les autres versions Unity restent à valider. L’interface IMGUI n’a pas été qualifiée pour lecteur d’écran ou navigation clavier complète. Voir TEST_CHECKLIST.md et AUDIT-UI-UX.md.
