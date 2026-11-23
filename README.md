# Novint Falcon sur Windows et Unity 6

## Projet complet prêt à tester

- [Projet Unity Falcon SDK Playground 1.1.0](https://github.com/baptisteauvert-fr/falcon-haptic-unity/releases/download/v1.1.0/Falcon-SDK-Playground-Unity-1.1.0.zip) : projet autonome, SDK embarqué, directions réglées par trois gestes et interface Appareil → Directions → Essayer. Ouvrir dans Unity 6000.3.24f1 puis lancer Play.
- [Application Windows x64 1.1.0](https://github.com/baptisteauvert-fr/falcon-haptic-unity/releases/download/v1.1.0/Falcon-SDK-Playground-Windows-x64-1.1.0.zip) : extraire entièrement et lancer `FalconPlayground.exe`, sans Unity.
- [Guide](Unity/FalconSdkPlayground-1.1/README.md) · [Audit UI/UX](Unity/FalconSdkPlayground-1.1/AUDIT-UI-UX.md) · [Parcours manuel](Unity/FalconSdkPlayground-1.1/TEST_CHECKLIST.md) · [Validation](Unity/FalconSdkPlayground-1.1/VALIDATION.md).

Le projet et les archives 1.0 sont conservés. La nouvelle version reste dans `Unity/FalconSdkPlayground-1.1` pour préserver l’ancien projet ouvert.

Le démarrage est en simulation, sans force armée. Le bouton matériel est explicite et demande le SDK fournisseur et le pilote installés séparément. Les essais automatisés du projet et du Player ont réussi en simulation ; les sensations et les coupures réelles restent à qualifier sur le Falcon.

## Préversion du SDK : directions calibrées et friction dynamique

Le package [com.falcon.haptics-1.2.0-preview.5.tgz](Distribution/com.falcon.haptics-1.2.0-preview.5.tgz) ajoute un repère de directions observées au mapping existant et conserve la friction dynamique de **Falcon Surface**. Le Playground guide les trois gestes pour corriger les permutations gauche/droite, haut/bas et profondeur. [Guide de la préversion](Unity/com.falcon.haptics/README.md) · [Tests reproductibles](Tests/README.md)

Cette préversion est distincte du package 1.1.0 inclus dans les installateurs ci-dessous. Les variantes FalconLab 4/8 N ne sont pas intégrées. Une seule face et une seule surface sont prises en charge ; les Rigidbody, l’adhérence statique et les contacts multiples restent hors périmètre. Les validations matérielles de 1.1.0 ne qualifient pas la stabilité ni la coupure des nouvelles interactions. La boîte calibrée reste une approximation logicielle, pas une certification des limites mécaniques.

## Fichiers à transmettre

- [Falcon-Setup-1.4.2-x64.exe](https://github.com/baptisteauvert-fr/falcon-haptic-unity/releases/download/v1.1.0/Falcon-Setup-1.4.2-x64.exe) installe le diagnostic et place le kit Unity dans son dossier **Unity**. Le raccourci **SDK Unity** du menu Démarrer ouvre ce dossier. Le lancement à l’ouverture de session est une option de l’installateur.
- [com.falcon.haptics-1.1.0.tgz](Distribution/com.falcon.haptics-1.1.0.tgz) est le package à importer dans Unity. Il est aussi inclus dans l’installateur.
- [Falcon-Diagnostics-1.4.2-x64.exe](https://github.com/baptisteauvert-fr/falcon-haptic-unity/releases/download/v1.1.0/Falcon-Diagnostics-1.4.2-x64.exe) est la version portable à lancer directement. Elle convient pour réparer une installation existante.

L’installateur s’exécute dans le compte utilisateur. Si le diagnostic portable est déjà ouvert, quitter via son icône près de l’horloge avant d’installer. Le diagnostic propose le SDK Force Dimension et son pilote si nécessaire. Pour une installation hors ligne, placer l’installateur officiel `sdk-3.17.7-win-x64.exe` à côté de **Falcon-Setup** avant de le lancer. [Téléchargement officiel](https://forcedimension.com/software)

## Utiliser le diagnostic

1. Ouvrir [Falcon Diagnostics](https://github.com/baptisteauvert-fr/falcon-haptic-unity/releases/download/v1.1.0/Falcon-Diagnostics-1.4.2-x64.exe). Sur un autre PC Windows x64, copier cet exécutable et l’ouvrir une première fois. Il contient .NET.
2. Brancher l’alimentation et le câble USB du Falcon. La fenêtre affiche l’état USB, le pilote et sa version, ainsi que le SDK DHD installé.
3. La lecture démarre au branchement. Après mise sous tension, tirer puis repousser doucement la poignée pour l’initialiser. Bouger sur les trois axes et presser chacun des quatre boutons. Une coche apparaît lorsqu’un axe a bougé d’au moins 1 mm ou qu’un bouton a répondu. **Lancer le test** relance la lecture après un arrêt ou une erreur.
4. Cliquer sur **Arrêter** avant de lancer un test dans Unity. Une seule application peut tenir la connexion WinUSB à la fois.

La croix ferme la fenêtre et laisse la surveillance dans la zone de notification. La case **Surveiller à l’ouverture de ma session Windows** active le lancement automatique dans la session courante. **Quitter**, dans le menu de l’icône, arrête la surveillance jusqu’au prochain lancement de l’application ou à la prochaine ouverture de session si la case reste cochée.

## Tester le retour de force

Dans **Retour de force**, choisir un mode et une intensité. Cliquer sur **Activer le mode**, puis maintenir **le bouton 1 de la poignée** en déplaçant doucement le Falcon. Commencer à faible intensité. Pour **Brique**, pousser la poignée vers l’appareil. Pour **Ressort**, la position au moment où le bouton est pressé sert de point de rappel.

| Mode | Sensation simulée |
| --- | --- |
| Eau | Le freinage augmente avec la vitesse. |
| Brique | Une paroi s’oppose à la poussée après environ 4 mm. |
| Résistance | L’effort s’oppose au mouvement dans toutes les directions, même à faible vitesse. |
| Ressort | La poignée est ramenée vers le point de rappel. |
| Gravier | Le frottement varie par petites ondulations pendant le déplacement. |
| Levier moteur | La poignée vibre au ralenti même immobile, avec un léger rappel vers son point de départ. |

Relâcher le bouton 1 arrête l’effort. **Couper les forces**, un changement de mode, la fermeture ou la perte de focus désarment également l’effet. À 100 % d’intensité, la commande maximale est de 1,8 N pour Eau, Brique et Résistance et de 1,2 N pour Ressort, Gravier et Levier moteur. Les mêmes modes sont disponibles dans le package Unity.

## Configurer un PC neuf

Le diagnostic v1.4.2 démarre sur Windows x64 avec .NET embarqué. Le Falcon déclenche la fenêtre dès que le diagnostic surveille les branchements. Lors de la première utilisation sur un PC, ouvrir l’exécutable puis cocher **Surveiller à l’ouverture de ma session Windows** pour les suivantes.

- **Installer le SDK officiel** télécharge l’installateur Force Dimension 3.17.7 x64 et vérifie son empreinte avant de l’ouvrir. Suivre la fenêtre du fournisseur et accepter la demande d’administration de Windows. Si Windows attend encore le pilote USB du Falcon, le diagnostic propose son installation.
- **Installer le pilote USB** apparaît si Windows détecte le Falcon sans pilote compatible et que le SDK est disponible. Le diagnostic utilise le package USB signé de la version 3.17.7 et vérifie sa présence. Windows demande l’autorisation d’installation.
- **Réparer le pilote du Falcon** apparaît si Windows utilise **FTDIBUS** pour le Falcon détecté. Quitter l’ancienne version du diagnostic depuis son icône près de l’horloge, fermer Unity et laisser un seul Falcon branché. Ouvrir le diagnostic v1.4.2, cliquer sur la réparation et accepter la demande de Windows. Le diagnostic cible l’identifiant USB `USB\VID_0403&PID_CB48`. Si Windows demande un redémarrage, redémarrer puis rouvrir l’application. Vérifier **Pilote · WINUSB**, puis cliquer sur **Lancer le test** et vérifier le firmware, les axes et les boutons.
- Pour préparer un PC hors ligne, télécharger `sdk-3.17.7-win-x64.exe` depuis Force Dimension et le placer à côté de `Falcon.Diagnostics.exe`. Le bouton **Installer le SDK officiel** vérifie et ouvre cette copie. Si le SDK est installé dans un dossier personnalisé, sélectionner son fichier `bin\dhd64.dll` avec **Choisir dhd64.dll**. [Téléchargement officiel](https://forcedimension.com/software)

Sur le poste de développement, le SDK 3.17.7 et le pilote WinUSB 3.18.0.3 sont installés. La lecture des axes et des boutons est disponible dès que ces deux composants sont utilisables.

## Utiliser le Falcon dans Unity

1. Ouvrir **Window → Package Manager**, puis **+ → Install package from tarball**.
2. Sélectionner [com.falcon.haptics-1.1.0.tgz](Distribution/com.falcon.haptics-1.1.0.tgz).
3. Ajouter **Input → Novint Falcon** à un GameObject et démarrer Play. Facultativement, assigner un objet séparé au champ **Cursor** pour visualiser le déplacement.
4. Importer **Falcon Monitor** depuis les exemples du package pour afficher la position, les boutons et essayer les effets dans la vue **Game**. [Guide de l’API Unity](Unity/com.falcon.haptics/README.md)

Le composant détecte le SDK local. Le champ **Dhd Library Path** ou la variable d’environnement `FALCON_DHD_DLL` permet de spécifier un autre chemin absolu. Le package cible Unity **2021.3 ou ultérieur** sur Windows x64. Le module Windows IL2CPP est absent des éditeurs vérifiés sur ce poste.

L’import du `.tgz` et la compilation du moniteur ont été vérifiés dans les éditeurs **6000.2.9f1**, **6000.3.24f1** et **6000.6.8f1**. Les Players Windows x64 Mono de **6000.3.24f1** et **6000.6.8f1** ont lu la position du Falcon réel. Les forces dans le Player Unity, les versions antérieures et IL2CPP restent à valider sur les plateformes correspondantes.

La communication et les entrées ont été vérifiées sur le Falcon réel. Le portage d’anciennes scènes qui appellent l’API HDAL reste à faire.

## Recompiler le diagnostic

```powershell
dotnet publish .\Falcon.Diagnostics\Falcon.Diagnostics.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\Application
Copy-Item .\Application\Falcon.Diagnostics.exe .\Distribution\Falcon-Diagnostics-1.4.2-x64.exe
npm.cmd pack .\Unity\com.falcon.haptics --pack-destination .\Distribution --ignore-scripts
ISCC.exe .\Installer\FalconSetup.iss
```

