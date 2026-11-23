# Validation du SDK Falcon

Ces tests ne chargent pas DHD et n’émettent aucune commande vers un Falcon.

Dernier compte rendu : [directions et parcours, 1.2.0-preview.5](VALIDATION-1.2.0-preview.5.md). Le compte rendu [friction dynamique, 1.2.0-preview.4](VALIDATION-1.2.0-preview.4.md) est conservé. Les comptes rendus de [première surface](VALIDATION-1.2.0-preview.3.md), [calibration](VALIDATION-1.2.0-preview.2.md) et [consolidation](VALIDATION.md) sont conservés.

## Noyau et concurrence

Sur Windows x64, avec le SDK .NET 10 :

```powershell
dotnet run --project Tests/Falcon.Core.Tests/Falcon.Core.Tests.csproj -c Release
```

Le programme renvoie un code non nul au premier échec. Il vérifie les forces invalides et les plafonds, le bouton d’autorisation, la cohérence des lectures concurrentes, les horodatages, l’arrêt d’une lecture bloquée, la reconnexion désarmée, le signal de vie, les lectures trop lentes, les erreurs de nettoyage et 100 créations/arrêts de worker. Le test de nettoyage DHD remplace les exports par des fonctions simulées et utilise uniquement un handle de `kernel32.dll` pour vérifier le déchargement ; il ne charge pas DHD. Les temporisations utilisent des délais tolérants ; ce n’est pas un banc de performance temps réel.

## Compilation des API Unity

Les tests du noyau vérifient aussi les conversions de positions/vitesses/forces (rotations, échelles et cisaillement), l’erreur aller-retour inférieure à un micromètre dans les cas testés, la conservation du travail virtuel, la calibration incomplète et les marges invalides. Le test de limites vérifie le désarmement dans le worker et l’absence de réarmement au retour dans la boîte.

Les tests de surface ajoutent contact répulsif et force nulle hors contact, plafond, montée limitée, atténuation aux bords, refus de départ derrière la face, pénétration excessive, amortissement sans attraction et expiration de géométrie avec signal de vie applicatif toujours actif.

La friction est testée sur 10 000 pas avec changements de direction dans un plan oblique : tangence, puissance non positive aux tolérances numériques, borne de Coulomb et plafond vectoriel commun. Les tests vérifient aussi la priorité normale, la transition à faible vitesse, l’arrêt, la sortie de contact, le coefficient nul, les valeurs invalides, la mutation désarmée et la remise à zéro du pic lors de l’arrêt de la prise.

```powershell
dotnet build Tests/Unity.ApiCheck/Unity.ApiCheck.csproj -c Release -p:UnityEditorPath="D:/Unity/6000.3.24f1/Editor"
dotnet build Falcon.Diagnostics/Falcon.Diagnostics.csproj -c Release
```

Le premier projet compile runtime, moniteur et intégration éditeur contre les assemblies de la version choisie, avec C# 9 et .NET Standard 2.1. Ce contrôle ne remplace pas un import UPM ni un Player IL2CPP.

## Quatre configurations réelles de Play Mode

Créer un projet Unity **isolé et jetable**, y importer le package local, puis copier `Tests/Unity/Editor/FalconValidation.cs` sous `Assets/Editor`. Lancer :

```powershell
& 'D:/Unity/6000.3.24f1/Editor/Unity.com' -batchmode -nographics -projectPath 'D:/chemin/vers/projet-de-test' -executeMethod FalconValidation.Run -logFile 'D:/chemin/vers/projet-de-test/validation.log'
```

Ne pas ajouter `-quit` : le script quitte lui-même avec le résultat. Il crée/remplace `Assets/FalconValidation.unity` et un profil `Assets/FalconValidationCalibration.asset` dans ce projet isolé. Il vérifie connexion simulée, XYZ/boutons, mapping monde avec parents transformés, recentrage et changement de repère désarmés, capture et profils, désactivation/réactivation, pause et sortie de Play dans les quatre combinaisons de rechargement du domaine/de la scène. Il restaure les options de Play Mode avant de quitter.

Résultat attendu : quatre messages `FALCON PLAY PASS`, puis `FALCON UNITY VALIDATION PASS` et code de sortie zéro. Le test ne valide pas les forces physiques, l’armement via le focus d’une fenêtre, le pilote ou les Players.

## Export des surfaces et scène d’exemple

Dans le même projet isolé, importer le sample **Surface Demo**, puis copier aussi `Tests/Unity/Editor/FalconSurfaceValidation.cs` sous `Assets/Editor`. Lancer Unity comme ci-dessus, avec `-executeMethod FalconSurfaceValidation.Run`. Le script vérifie les six faces avec des repères transformés, l’exclusivité et la libération d’une surface, puis le refus des Rigidbody. Il crée/remplace `Assets/FalconSurfaceDemo.unity`, puis enchaîne les quatre configurations Play Mode. Ces dernières vérifient aussi la publication d’une surface et la préparation de son armement, sans bouton maintenu ni activation de moteur.

La scène distribuée est la copie de cette scène générée dans le projet isolé. L’export numérique et les calculs de force sont automatisés ; le focus et l’armement interactif de la vue Game restent à essayer manuellement.

Pour vérifier les références de la scène livrée, la copier sous `Assets/DistributedFalconSurfaceDemo.unity` dans ce projet isolé (sans dupliquer sa métadonnée de scène), conserver le script du sample et sa métadonnée, puis lancer `FalconSurfaceValidation.CheckDistributedDemo`. Ce contrôle exige zéro script manquant, le backend Simulation, les références du curseur et du contrôleur et une géométrie valide.

## Validation matérielle restante

Avant de qualifier la préversion, vérifier avec le Falcon : sens des axes et forces, initialisation, limites suivant la position, boutons, relâchement, déconnexion USB, pertes de focus, arrêt de Play, fermeture et blocage du processus. Mesurer la cadence effective et les délais de coupure. Tester séparément les Players Mono et IL2CPP. Aucun essai automatique ci-dessus ne garantit une coupure matérielle lors d’un blocage DHD.
