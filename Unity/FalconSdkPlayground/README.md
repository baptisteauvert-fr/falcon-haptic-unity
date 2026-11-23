# Falcon SDK Playground

Projet Unity autonome pour essayer **toutes les fonctionnalités actuellement implémentées** du SDK `com.falcon.haptics 1.2.0-preview.4`. Les sources du SDK sont embarquées dans `Packages/com.falcon.haptics` : aucune référence au dépôt d’origine n’est nécessaire.

## Ouvrir et démarrer

1. Extraire l’archive du projet dans un dossier accessible en écriture.
2. Dans **Unity Hub → Projects → Add project from disk**, sélectionner le dossier qui contient `Assets`, `Packages` et `ProjectSettings`.
3. Ouvrir avec **Unity 6.3, 6000.3.24f1**, version utilisée pour la création et les tests. Le projet utilise le rendu intégré et des modules Unity intégrés ; aucun Asset Store ou compte supplémentaire n’est requis.
4. La scène principale s’ouvre automatiquement si aucune scène de travail n’est ouverte. Sinon, ouvrir **Assets/Scenes/FalconPlayground.unity**, ou **Tools → Falcon → Ouvrir le banc d’essai**.
5. Cliquer sur **Play**, puis dans la vue **Game**. Le projet démarre en **simulation**, sans effet armé.

Une application Windows x64 est aussi proposée séparément : extraire son archive entière et lancer `FalconPlayground.exe`. Conserver les dossiers de données et DLL à côté de l’exécutable.

## Panneau d’essai

Le bouton **COUPER TOUTES LES FORCES** reste visible. Changer d’onglet désarme les effets. Le réglage d’intensité commence à 20 % ; l’armement et le bouton 1 restent obligatoires pour produire un effort.

| Onglet | Essais disponibles |
| --- | --- |
| Entrées | Position brute en mm, position dans la scène, quatre boutons, étendue observée sur chaque axe |
| Six effets | Eau, paroi selon X brut, résistance, ressort, gravier, moteur ; sélection et armement séparés |
| Surface | Activation du collider, six orientations de face, placement devant l’outil, friction, armement du contact |
| Calibration | Recentrage, capture des trois axes, échelle, rotation du repère, inversion de Z, limites, profils et sauvegarde JSON |
| Diagnostic | Connexion, âge de lecture, séquence, intervalles, erreur, relance de lecture et export JSON |

Les commandes du simulateur sont accessibles sous chaque onglet : X/Y/Z, quatre boutons, disponibilité du périphérique et balayage automatique. Le défilement donne accès aux contrôles en bas du panneau sur une petite fenêtre.

## Premier retour de force simulé

1. Dans **Six effets**, choisir **Ressort** et cliquer sur **Préparer cet effet**, avec le bouton 1 simulé relâché.
2. Cocher **Bouton 1**, puis déplacer doucement X ou Y.
3. Observer la commande simulée et la ligne rose près du curseur. Décocher **Bouton 1** pour couper.
4. Essayer les cinq autres modes. Pour **Paroi X**, diminuer X de plus de 4 mm après la prise. Pour **Moteur**, une vibration existe aussi à l’arrêt.

Les marqueurs d’activation suivent le parcours ; ils ne certifient pas les forces physiques.

## Surface et friction

1. Dans **Surface**, garder la face `−Z` pour le premier essai.
2. Cliquer sur **Réinitialiser la simulation** : Z brut revient à +6 mm, devant la face.
3. Cliquer sur **Préparer le contact de surface**, relâcher puis maintenir **Bouton 1**.
4. Réduire Z vers +2 mm puis +1 mm. Le rayon d’outil est de 2 mm : le contact commence avant que son centre atteigne le plan à Z = 0.
5. Déplacer X ou Y pour tester la friction. Le pic sur la prise permet d’observer les commandes brèves entre les trames simulées.
6. Modifier le coefficient de friction, puis réarmer explicitement. Zéro donne une surface sans friction.

**Placer la face 6 mm devant l’outil** déplace uniquement la surface virtuelle. Ce bouton aide à trouver une position de départ adaptée, y compris après calibration ou choix d’une autre face. Il ne commande aucun mouvement au Falcon.

Dans l’éditeur, **Sélectionner la surface dans l’Inspecteur** donne accès à la rigidité, l’amortissement, la force maximale, le rayon, l’atténuation aux bords, la pénétration maximale et la vitesse de régularisation de friction. La perte de focus et les modifications désarment : revenir dans Game et réarmer. Ces réglages détaillés restent dans l’Inspecteur ; ils ne sont pas tous exposés dans le Player.

## Calibration et mapping

1. Commencer par **Recentrer ici** pour déplacer l’origine physique dans le repère de scène.
2. Cliquer sur **Commencer la capture des trois axes**, puis déplacer les trois curseurs simulés ou utiliser le balayage automatique.
3. Après au moins dix lectures et des mouvements suffisants sur les trois axes, cliquer sur **Terminer et appliquer**.
4. La boîte orange représente les limites logicielles. Sortir de cette boîte désarme ; le retour n’active pas automatiquement les forces.
5. Tester échelle, rotation et inversion de Z. Toute modification désarme.
6. **Enregistrer** puis **Recharger** vérifie la persistance. Les fichiers simulation et matériel sont distincts et ne sont jamais chargés automatiquement lors d’un changement de périphérique.

La boîte ne certifie pas la course mécanique ni l’accessibilité de ses coins. Ne pas forcer les butées. Le profil fourni est un exemple d’origine zéro sans limites, pas une calibration d’usine.

Les paramètres par axe, les marges de capture et les profils `ScriptableObject` sont aussi disponibles via l’Inspecteur du Workspace. **Enregistrer dans un nouveau profil** permet de conserver un asset de calibration depuis Play ; le recharger hors Play pour l’appliquer durablement à la scène.

## Utiliser le Falcon réel

1. Utiliser Windows x64 avec le SDK Force Dimension DHD et le pilote compatible installés. Les bibliothèques propriétaires et le pilote ne sont pas inclus dans ce projet. [Téléchargement fournisseur](https://forcedimension.com/software).
2. Fermer ou arrêter la lecture dans Falcon Diagnostics et les autres applications qui utilisent l’appareil.
3. Brancher le Falcon, puis cliquer sur **Connecter le Falcon réel**. L’ancien lecteur est arrêté avant l’ouverture du suivant. Aucun effet n’est armé automatiquement.
4. Vérifier d’abord la lecture des axes et boutons. Effectuer l’initialisation requise par le contrôleur, puis calibrer l’espace.
5. Commencer les essais à faible intensité, en restant dans une zone confortable. Préparer un effet, relâcher puis maintenir le bouton 1 physique.
6. Pour la surface, utiliser le placement devant l’outil avant de réarmer. Revenir devant la face si l’activation a été refusée derrière celle-ci.

Si DHD est installé ailleurs, sélectionner l’objet matériel inactif dans la Hierarchy et renseigner **Dhd Library Path** avant connexion, ou configurer `FALCON_DHD_DLL`. Le SDK recherche sinon son installation locale.

La coupure après un blocage natif ou un arrêt brutal, les axes et les sensations réelles restent à qualifier avec le Falcon. La simulation ne valide pas ces comportements.

## Fichiers et maintenance

- `Assets/Scenes/FalconPlayground.unity` : scène principale.
- `Assets/Playground/Runtime` : interface, commandes et représentations.
- `Assets/Playground/Editor` : générateur de scène et contrôle automatique en simulation.
- `Assets/Calibration/ExampleCalibration.asset` : exemple sans limites.
- `Packages/com.falcon.haptics` : sources embarquées du SDK, son guide et ses exemples.
- `TEST_CHECKLIST.md` : parcours manuel détaillé.
- `VALIDATION.md` : vérifications réellement exécutées et limites.

Les sauvegardes et exports de l’application vont dans `Application.persistentDataPath`, généralement `%USERPROFILE%/AppData/LocalLow/Falcon SDK/Falcon SDK Playground` sous Windows. Le panneau affiche le chemin exact. La sauvegarde JSON conserve origine et limites physiques ; elle ne sauvegarde pas échelle, orientation, matériau ou géométrie de scène.

En Play, les modifications de scène et des contrôles sont temporaires. Modifier et enregistrer la scène hors Play pour changer les valeurs de départ ; utiliser les sauvegardes de calibration pour les données physiques.

Ce banc couvre les fonctions présentes : il n’ajoute pas de mélange d’effets, d’adhérence statique, de contacts multiples, de corps mobiles ni de compatibilité IL2CPP qualifiée. Le SDK reste une préversion.
