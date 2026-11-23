# Novint Falcon pour Unity

Le package `com.falcon.haptics` fournit une API C# pour les trois axes, les quatre boutons et les six modes haptiques du Novint Falcon. Le backend matériel nécessite Windows x64 et Force Dimension DHD x64. Un backend simulé permet de tester sans appareil ni SDK DHD. Sur le même PC, cliquer sur **Arrêter** dans Falcon Diagnostics pour libérer le périphérique avant d’entrer en Play Mode.

La version **1.2.0-preview.5** ajoute les directions observées `FalconAxisAlignment` au mapping, aux profils et aux gizmos. Elle conserve surface avec friction dynamique, calibration et simulation. Elle ne reprend pas les variantes expérimentales 4/8 N ou le rappel après relâchement de FalconLab.

`FalconAxisAlignment.TryFromGestures` reçoit trois déplacements bruts en mètres : droite, haut, vers l’utilisateur. Les gestes ambigus ou inférieurs à 5 mm sont refusés. Appliquer le résultat avec `FalconWorkspace.SetAxisAlignment` ; cette opération désarme. Le mapping des forces reste conjugué aux déplacements. Sans alignement explicite, le mapping existant reste inchangé. Le [Playground 1.1](../FalconSdkPlayground-1.1/README.md) propose le parcours guidé et la sauvegarde complète de l’application.

## Importer

1. Dans Unity, ouvrir **Window → Package Manager** et utiliser **+ → Install package from tarball**. Sélectionner `com.falcon.haptics-1.2.0-preview.5.tgz`. Pour développer depuis les sources, choisir **Install package from disk** et le fichier `package.json` de ce dossier.
2. Sur un GameObject, ajouter le composant **Input → Novint Falcon**. Ajouter facultativement un objet distinct dans **Cursor**.
3. Pour une interface de test, importer **Falcon Monitor** depuis l’onglet **Samples** du package. Ajouter le composant **Falcon Monitor** à un GameObject, lancer Play, puis cliquer dans la vue **Game**. Le composant ajoute automatiquement `FalconInput` s’il manque.

## API

- `FalconInput.Connected`, `PositionMetres`, `Buttons`, `IsButtonPressed(0…3)` et `Status` donnent l’état de lecture.
- `TryArmEffect(HapticMode.Water, 30)` prépare un effet à 30 %. L’utilisateur doit relâcher puis maintenir le bouton 1 de la poignée pour recevoir l’effort. La méthode retourne `false` sans connexion ou sans focus.
- `EffectArmed`, `EffectActive` et `EffectError` permettent de présenter l’état ou le dernier problème. `SetEffectIntensity(0…100)` modifie le gain et `DisarmEffect()` arrête l’effet.
- Modes disponibles dans `HapticMode` · `Water`, `Brick`, `Resistance`, `Spring`, `Gravel`, `EngineLever`.
- `PositionMetres` est exprimée dans le repère brut du Falcon. Sans `FalconWorkspace`, le curseur conserve le mapping historique : inversion de Z et **Unity Units Per Metre** dans son repère parent. Avec `FalconWorkspace`, son placement utilise le mapping monde décrit ci-dessous.
- `ConnectionState`, `SampleSequence` et `SampleAgeSeconds` distinguent l’état de session et la fraîcheur de la dernière lecture. Au-delà de 100 ms, la lecture devient périmée et `Connected` est faux. Cet âge mesure la réception côté logiciel, pas l’âge d’une mesure interne au contrôleur.
- `MeanIntervalMilliseconds` et `MaximumIntervalMilliseconds` mesurent les intervalles entre lectures réussies sur la session. Ces statistiques incluent les attentes volontaires de 8 ms au repos et 2 ms lorsqu’un effet est armé ; elles ne constituent pas une validation à 1 kHz.

## Essayer sans Falcon

1. Ajouter **Input → Novint Falcon** et sélectionner **Backend → Simulation** avant Play.
2. Lancer Play. Modifier **Simulation Position Metres** et le masque **Simulation Buttons** dans l’inspecteur.
3. Pour essayer le ressort dans le moniteur : laisser les boutons à zéro, activer le ressort, puis passer le masque à `1` et déplacer la position simulée. Garder la vue Game au premier plan pour l’armement et le signal de vie.
4. Lire **SimulatedForceNewtons** par l’API ou dans le moniteur ; cette force est enregistrée, jamais appliquée à un moteur. Remettre le masque à zéro arrête l’effort.
5. Décocher **Simulation Available** injecte une déconnexion. La reconnexion revient sans effet armé.

`SetSimulationSample(Vector3 positionMetres, uint buttons)` et `SetSimulationAvailable(bool)` permettent de piloter ces entrées depuis un script Unity sur le thread principal. Le bit 0 du masque correspond au bouton 1, le bit 3 au bouton 4. Le simulateur ne modélise pas la mécanique du Falcon ni la réaction de l’utilisateur.

## Calibrer et placer la zone dans la scène

1. Sur le même objet que `FalconInput`, ajouter **Input → Falcon Workspace**.
2. Déplacer et orienter le **Transform de cet objet** pour placer le repère dans la scène. Les transformations parentes sont prises en compte. Le curseur doit être distinct du repère et de ses parents ; il peut être un enfant.
3. Régler **Units Per Metre** par axe. Une valeur de 10 transforme 1 cm physique en 0,1 unité locale Unity, avant les transformations parentes. **Invert Z** conserve l’inversion historique de Z par défaut.
4. En Play, avec une lecture récente, cliquer sur **Recentrer sur la position actuelle** : cette position brute devient l’origine du repère de scène.
5. Cliquer sur **Commencer la capture de déplacement**, déplacer doucement la poignée sur les trois axes, puis **Terminer et appliquer la calibration**. Pendant la capture, les effets sont désarmés et leur armement par `FalconInput` est interdit.
6. Examiner la boîte orange et les axes rouge/vert/bleu dans la vue Scene, objet sélectionné. Pour conserver le résultat, cliquer sur **Enregistrer dans un nouveau profil…**. Après la sortie de Play, assigner ce profil au composant puis cliquer sur **Charger le profil assigné**.

La capture exige au moins dix lectures distinctes et une étendue minimale sur chaque axe. Les valeurs proposées (5 mm d’étendue minimale et 1 mm de marge intérieure) sont des critères logiciels modifiables, **pas des caractéristiques ni des seuils de sécurité du Falcon**. Une perte de lecture ou un changement de mapping annule la capture.

La boîte utilise les minima/maxima observés, réduits par la marge choisie. Elle ne prouve pas que tous ses coins soient atteignables. Ne poussez pas le Falcon contre ses butées pour remplir la boîte. L’initialisation du contrôleur reste distincte de cette calibration utilisateur.

**Use Physical Bounds** active une limite logicielle en mètres bruts. Hors de cette boîte, le worker désarme l’effet et refuse un nouvel armement ; revenir dans la boîte ne réarme rien. **Clamp Cursor To Bounds** limite uniquement la représentation visuelle : ce réglage ne crée ni surface ni force de rappel. Sans limites configurées, aucune boîte matérielle n’est supposée.

Changer l’origine, le Transform (y compris un parent), l’échelle, l’inversion de Z ou les limites désarme les effets. Le repère doit rester fixe pendant une interaction armée. Avec ce composant, le champ historique **Unity Units Per Metre** de `FalconInput` n’est plus utilisé. Les profils enregistrent l’origine et la boîte physiques ; ils ne modifient pas le Transform de la scène, l’échelle ou l’inversion de Z. Leur chargement est explicite, sans modification automatique d’un profil partagé.

### API de mapping

`TryRecenter`, `BeginCapture`, `CompleteCapture` et `CancelCapture` pilotent la calibration sur le thread principal. `SetCalibration`, `SetScale`, `LoadProfile` et `CopyCalibrationTo` servent aux scripts. `CopyCalibrationTo` copie en mémoire ; utiliser les boutons de l’inspecteur pour enregistrer un asset sur disque. `MappedPositionWorld`, `IsInsidePhysicalBounds`, `Status` et `CaptureStatus` donnent les diagnostics.

```csharp
var workspace = GetComponent<FalconWorkspace>();
if (workspace.TryMapPosition(falcon.PositionMetres, out var worldPosition))
    cursor.position = worldPosition;
```

Le curseur assigné à `FalconInput` est déjà placé automatiquement ; cet exemple sert à une intégration personnalisée. `TryWorldToDevicePosition` donne la conversion inverse sans clamp. Après une limitation visuelle, cette inversion retrouve le point limité et non la position brute initiale.

`FalconWorkspaceMap` fournit les mathématiques sans dépendance Unity. Pour `x_world = A x_device + b`, `WorldToDeviceForce` applique `Aᵀ` pour conserver le travail virtuel. L’entrée est une force conjuguée aux déplacements de scène (J/unité Unity), la sortie est en newtons matériels. Avec une échelle uniforme de 10, une force conjuguée de 0,1 correspond à 1 N avant plafonnement. `TryWorldToDeviceForce` expose cette conversion avec des `Vector3` Unity ; elle n’envoie aucune commande au Falcon.

Les six effets existants restent calculés dans le repère matériel, avec leurs plafonds précédents ; ils ne sont pas rescalés par le mapping d’affichage. La première surface convertit sa géométrie dans ce repère physique et y calcule sa rigidité et son amortissement ; elle n’applique pas une seconde multiplication d’échelle à sa force.

## Première surface liée à un collider

Pour un essai immédiat, importer **Surface Demo** depuis les Samples et ouvrir sa scène. Les curseurs de la vue Game pilotent uniquement le simulateur et affichent la force calculée.

Pour équiper sa propre scène : ajouter `FalconWorkspace` au Falcon, créer un objet avec `BoxCollider`, puis ajouter **Input → Falcon Surface** sur cet objet. Assigner le Workspace, choisir la face et examiner la flèche cyan dans Scene. Placer l’outil devant cette face, activer via `surface.TryArm(30)` ou `falcon.TryArmSurface(30)`, puis relâcher et maintenir le bouton 1. `DisarmEffect()` ou `surface.Disarm()` arrête l’interaction. Le moniteur `FalconInput.SurfaceContact` distingue le contact géométrique de l’état d’activation des moteurs.

La surface est une face finie, à sens unique ; elle ne reproduit pas les six faces complètes du collider. Son export tient compte de son centre, de sa taille, de ses transformations parentes et du mapping. Le calcul reçoit ces données en mètres physiques, sans accès aux colliders depuis le worker et sans dépendre de `OnCollision` ou du pas de physique Unity.

Les paramètres sont **Stiffness** en N/m, **Damping** en N·s/m, **Tool Radius**, **Edge Fade** et **Maximum Penetration** en mètres physiques, ainsi que le plafond en N et la vitesse de montée en N/s. Ils sont indépendants de l’échelle d’affichage. Les valeurs proposées sont des réglages logiciels de départ, à qualifier sur le matériel. La force maximale de cette voie reste au plus 1,2 N, avec le même plafond DHD que le mode ressort historique.

Le contact applique une pénalité de ressort amorti uniquement en pénétration et sans attraction à la sortie. Les bords sont atténués ; ils ne constituent pas des obstacles sphériques. Le curseur reste la position mesurée, sans proxy contraint à la surface. La friction dynamique est facultative ; adhérence statique, rigidité parfaite, réaction sur un Rigidbody et contacts multiples restent hors périmètre.

### Friction dynamique

**Friction Coefficient** est un coefficient sans unité, nul par défaut pour préserver les surfaces existantes. **Friction Speed Metres Per Second** fixe une vitesse de régularisation strictement positive (0,005 m/s par défaut, un réglage logiciel). La friction s’oppose à la vitesse tangentielle courante et tend continûment vers zéro près de l’arrêt. Elle ne simule pas un collage statique et ne conserve pas de force tangentielle si la position ne change plus.

La cible avant plafonnement est `-μ Fn vt / sqrt(|vt|² + v0²)`. `Fn` est la charge normale effectivement commandée, après gain et rampe. Le contact normal est prioritaire ; la friction reçoit seulement le budget vectoriel restant `sqrt(Fmax² - Fn²)`. Une normale qui atteint le plafond peut donc ne laisser aucun frottement. Le plafond n’est jamais appliqué séparément à deux forces qui seraient ensuite additionnées sans contrôle.

La projection de la commande tangentielle précédente ne peut être réutilisée que si elle s’oppose au mouvement courant. Arrêt, sortie de contact, diminution de charge ou inversion peuvent supprimer immédiatement une composante ; la limite de montée n’est pas une garantie de continuité vectorielle sur ces changements. Cette protection logicielle ne qualifie pas la stabilité physique du système échantillonné.

`surface.SetFriction(0.5f, 0.005f)` règle les paramètres sur le thread Unity et désarme si une valeur change. Réarmer explicitement après modification. `FalconInput.SurfaceFrictionPeakNewtons` affiche le pic calculé et envoyé avec succès au backend sur la prise courante ; il est remis à zéro au relâchement ou à l’arrêt. C’est un diagnostic de commande, pas une mesure physique de force.

Dans **Surface Demo**, la friction démarre à 0,5 ; le bouton de comparaison permet de la couper puis de la réactiver. Après chaque changement, replacer l’outil devant la face et réarmer. En contact, déplacer X ou Y. Les entrées simulées changent par trames, donc des commandes de friction peuvent être très brèves ; le pic permet de les voir même lorsque la commande instantanée est revenue à zéro.

Toute modification de la géométrie ou du matériau désarme l’effet ; les déplacements animés continus ne sont donc pas pris en charge. Un `Rigidbody`, même cinématique ou porté par un parent, est refusé. Le worker désarme aussi sur données de surface âgées de plus de 100 ms, pénétration excessive, perte d’autorisation ou intervalle irrégulier. La réduction de force et la sortie de contact peuvent aller immédiatement à zéro ; seule la montée est limitée. Un blocage du worker conserve les limitations de coupure décrites plus bas.

Une seule surface peut être associée à un Falcon dans cette préversion. Son armement remplace le mode haptique précédent et inversement ; les forces ne sont pas additionnées. Après une erreur, revenir devant la face, relâcher et réarmer explicitement. L’armement prépare une interaction ; il n’implique pas qu’une force soit déjà appliquée.

## Sessions et arrêt

`IFalconBackend` sépare le transport et le calcul d’effets. `FalconSession` possède son backend sur un thread dédié ; la factory fournie à son constructeur y est exécutée. Elle ne doit pas accéder à des objets Unity. `SimulatedFalconBackend.SetSample`, `SetAvailable` et `CommandedForce` sont synchronisés pour les échanges avec le thread appelant.

`FalconSession.State` renvoie une copie cohérente, sans allocation de snapshot par lecture. Les six effets et leurs plafonds actuels (1,2 ou 1,8 N) restent identiques. Une absence de signal de vie pendant 240 ms désarme l’effet ; une lecture ou un intervalle supérieur à 45 ms pendant l’armement provoque une erreur et un désarmement. Ces seuils logiciels ne sont pas des limites matérielles certifiées.

`RequestStop()` demande l’arrêt. `Stop(timeoutMilliseconds)` retourne `true` uniquement après terminaison du worker. Il faut aussi examiner `State.EffectError` : un thread terminé ne prouve pas à lui seul la réussite de la coupure matérielle. `Dispose()` lève une exception si le worker ne s’arrête pas dans le délai. En cas de délai dépassé, la session garde le backend ; aucun autre thread ne le ferme ou ne décharge sa DLL.

Le backend DHD refuse une deuxième connexion dans le même processus. Ses opérations doivent rester sur le thread où il a été construit. La fermeture tente zéro, désactivation, fermeture et déchargement, en remontant les erreurs. Si la fermeture native échoue, le handle et la DLL restent réservés et la reconnexion automatique est interrompue ; une intervention et éventuellement un redémarrage du processus sont nécessaires.

L’intégration éditeur demande l’arrêt avant rechargement d’assemblies, sortie de Play et fermeture ; elle désarme lors d’une pause de l’éditeur. Elle relance les composants actifs à l’entrée en Play, y compris sans rechargement du domaine ou de la scène.

**Limite importante :** si un appel DHD se bloque, le code peut constater une lecture périmée et un arrêt non confirmé, mais ne peut pas imposer une force nulle à travers cet appel. Le comportement du pilote, du firmware et d’un éventuel watchdog doit être mesuré sur un Falcon réel. Le simulateur ne valide pas cette protection.

## Compatibilité

Le minimum déclaré reste Unity 2021.3 et les API .NET Standard 2.1 ; ce minimum n’est pas encore validé dans un éditeur 2021.3. Le backend matériel cible un éditeur ou Player Windows x64. Le SDK officiel et le pilote restent installés sur le PC. Le package contient les sources C# et utilise `dhd64.dll` à partir de l’installation officielle. Le champ **Dhd Library Path** accepte son chemin absolu si nécessaire. [Téléchargement du SDK Force Dimension](https://forcedimension.com/software)

Les validations matérielles historiques de 1.1.0 ne qualifient pas automatiquement cette préversion. IL2CPP, les autres systèmes, les forces et les scénarios d’incident réels restent à valider. Les tests reproductibles sont décrits dans `Tests/README.md` à la racine du dépôt.

